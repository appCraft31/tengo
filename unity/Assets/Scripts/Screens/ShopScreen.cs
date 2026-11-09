using System;
using System.Collections.Generic;
using System.Linq;
using TenGo.App;
using TenGo.Core;
using TenGo.Fx;
using TenGo.Game;
using TenGo.Haptics;
using TenGo.Rendering;
using TenGo.Services;
using TenGo.Ui;
using TMPro;
using UnityEngine;

namespace TenGo.Screens
{
    /// Boutique (BoutiqueScene.swift), refaite pour la 4.0
    /// (`design/refonte/04_boutique`) : en-tête fixe (titre, solde) et
    /// pastilles de rubrique qui font défiler jusqu'à la section ; « Sans
    /// pub » en bandeau sobre ; thèmes en grille avec un vrai aperçu de grille
    /// 4 × 2 aux couleurs du thème (le thème équipé cerclé d'encre) ; matières,
    /// tracés et boosters en listes compactes (prix grisé si les pièces
    /// manquent) ; packs de pièces en grille. Achat avec les pièces gagnées,
    /// sélection du cosmétique actif ; les achats en vraie monnaie passent par
    /// `IAdFreeStore` / `ICoinStore`. Ouverte depuis une partie, un bouton de
    /// retour en haut à gauche remplace les onglets. Mode guidé : premier
    /// achat d'un indice, remboursé.
    public sealed class ShopScreen : ScreenBase
    {
        const int Z = 20;
        static readonly string[] SectionKeys = { "themes", "bubbles", "trails", "boosters", "coins" };
        static readonly string[] SectionTitles = { "shop.tab_themes", "shop.tab_bubbles", "shop.tab_trails", "shop.tab_boosters", "shop.tab_coins" };

        readonly bool _returnToGame;
        bool _guided;
        float _cw;
        float _viewportBottom;
        readonly Dictionary<string, float> _sectionTops = new();
        readonly List<(ShapeView bg, ShapeView hair, TenGoText label)> _pills = new();
        int _activePill = -1;
        Vector2 _balanceChipCenter;
        bool _attemptedProductLoad;
        bool _isLoadingProducts;
        CoachMarkOverlay _purchaseGuide;
        Transform _guideLater;
        bool _guideTouch;
        Transform _toast;

        /// Guide d'achat affiché (tests).
        public bool GuideVisible => _purchaseGuide != null;
        /// Dernier message de la bulle d'information (tests).
        public string LastToast { get; private set; }
        public bool ReturnToGame => _returnToGame;

        static float Pt(float v) => Design.Pt(v);

        public ShopScreen(AppRoot app, bool returnToGame, bool guided) : base(app, "ShopScreen", themedBackground: false)
        {
            _returnToGame = returnToGame;
            _guided = guided;
            TapOnRelease = true;
            // Prix « sans pub » inconnu : on le redemande, la page se reconstruit.
            if (!S.AdFree.IsPurchased && S.AdFreeStore.DisplayPrice == null) S.AdFreeStore.LoadProducts();
            Rebuild();
            S.Analytics.ShopOpened(returnToGame ? "game" : "tab");
            if (!S.AdFree.IsPurchased) S.Analytics.NoAdsOfferView("shop");
        }

        /// Défile pour amener le haut d'une section en haut de la zone visible.
        public void ScrollToSection(string key)
        {
            if (_sectionTops.TryGetValue(key, out float top)) Scroll.ScrollTo(top);
            UpdatePills();
        }

        public IReadOnlyDictionary<string, float> SectionTops => _sectionTops;

        // MARK: - Mise en page

        /// Tout est reconstruit (`removeAllChildren`) : fond, chrome et contenu,
        /// en gardant le défilement. Toujours afficher les toasts APRÈS.
        void Rebuild()
        {
            float previousOffset = Scroll?.Offset ?? 0;
            for (int i = Root.childCount - 1; i >= 0; i--) RenderShared.Destroy(Root.GetChild(i).gameObject);
            Targets.Clear();
            _pills.Clear();
            _activePill = -1;
            _purchaseGuide = null;
            _guideLater = null;
            _toast = null;
            TabBarNode = null;

            App.SetBackground(Look.Background);
            ThemeBackground.Make(Root, Look.Theme, SceneSize);

            float w = UsableW;
            _cw = w - 2 * Pt(Design.Gutter);
            float left = -_cw / 2, right = _cw / 2;
            float rowY = HalfH - TopInset - Pt(32);

            // En-tête fixe : retour (depuis une partie), titre, solde.
            float titleX = left;
            if (_returnToGame)
            {
                var back = Design.IconButton("shopBack", Root, new Vector2(left + Pt(22), rowY), VectorIconName.LineBack, Z + 12);
                AddTarget("shopBack", back, Design.Pt(48, 48), GoBack, animate: false);
                titleX = left + Pt(44 + 12);
            }
            float chipW = AddBalanceChip(right, rowY);
            Design.Text(Root, Loc.T("shop.title"), NunitoWeight.Black, 32, Design.Ink, new Vector2(titleX, rowY), Z + 12,
                HorizontalAlignmentOptions.Left).ShrinkToFit(right - chipW - Pt(12) - titleX);

            // Pastilles de rubrique.
            float pillsY = rowY - Pt(32 + 14 + 20);
            AddSectionPills(left, pillsY);
            float barBottom = pillsY - Pt(20 + 12);
            ShapeView.Rect("HeaderBg", Root, Units.P(0, (HalfH + barBottom) / 2 + Pt(40)), new Vector2(w + 40, HalfH - barBottom + Pt(80)), 0,
                Design.Bg, default, 0, Z + 8);
            ShapeView.Rect("HeaderLine", Root, Units.P(0, barBottom), new Vector2(w + 40, Pt(1)), 0, Design.Line, default, 0, Z + 9);

            // Ouverte depuis l'accueil, la boutique EST l'onglet Boutique ; depuis
            // une partie, elle garde son bouton de retour (les onglets abandonneraient la grille).
            if (!_returnToGame)
            {
                AddTabBar(Tab.Shop);
                _viewportBottom = TabBarTop;
            }
            else _viewportBottom = -HalfH + BottomInset;

            Scroll = new ScrollArea(Root, barBottom, _viewportBottom, -w / 2, w / 2);
            BuildContent();
            Scroll.ApplyClip();
            Scroll.SetOffset(previousOffset);
            UpdatePills();

            if (_guided) AttachGuide();
        }

        void AddSectionPills(float left, float y)
        {
            float x = left;
            for (int i = 0; i < SectionKeys.Length; i++)
            {
                string key = SectionKeys[i];
                var node = RenderShared.Node("section:" + key, Root, Units.P(0, y));
                var label = Design.Text(node, Loc.T(SectionTitles[i]), NunitoWeight.ExtraBold, 15, Design.Ink, Vector2.zero, Z + 13);
                float pw = label.WidthPt() + Pt(36), ph = Pt(40);
                node.localPosition = Units.P(x + pw / 2, y);
                var hair = ShapeView.Rect("Hair", node, Units.P(0, -Pt(1)), new Vector2(pw, ph), ph / 2, Design.Hair, default, 0, Z + 10);
                var bg = ShapeView.Rect("Bg", node, Vector2.zero, new Vector2(pw, ph), ph / 2, Design.Card, default, 0, Z + 11);
                _pills.Add((bg, hair, label));
                AddTarget("section:" + key, node, new Vector2(pw, ph), () => { HapticManager.Light(); ScrollToSection(key); }, animate: false);
                x += pw + Pt(8);
            }
        }

        /// La pastille de la section en haut de la zone visible passe à l'encre.
        void UpdatePills()
        {
            if (Scroll == null || _pills.Count == 0) return;
            int active = 0;
            for (int i = 0; i < SectionKeys.Length; i++)
                if (_sectionTops.TryGetValue(SectionKeys[i], out float top) && -top <= Scroll.Offset + Pt(40)) active = i;
            if (active == _activePill) return;
            _activePill = active;
            for (int i = 0; i < _pills.Count; i++)
            {
                bool on = i == active;
                _pills[i].bg.Fill = on ? Design.Ink : Design.Card;
                _pills[i].hair.Fill = on ? Design.Ink : Design.Hair;
                _pills[i].label.Color = on ? Design.Bg : Design.Ink;
            }
        }

        public override void Tick(float dt)
        {
            base.Tick(dt);
            UpdatePills();
        }

        void AttachGuide()
        {
            ScrollToSection("boosters");
            Scroll.Locked = true;
            // Cible la pilule ×1 : le tutoriel fait acheter UNE unité, pas le lot.
            var hintId = Booster.Hint.Id();
            var target = FindTarget($"booster:{hintId}:1")?.Node ?? FindTarget($"booster:{hintId}")?.Node;
            if (target == null) { Scroll.Locked = false; return; }
            _purchaseGuide = CoachMarkOverlay.Present(Root, target, Loc.T("guide.shop_item"), SceneSize);
            // Insistance : taper à côté ne ferme pas le guide ; « Plus tard » et le retour restent actifs.
            _purchaseGuide.BlocksOutsideTaps = true;
            AddGuideLaterButton();
        }

        void AddGuideLaterButton()
        {
            var label = TenGoText.Create(Root, Loc.T("guide.shop_later"), NunitoWeight.ExtraBold, Pt(16), Colors.White(0.98f, 0.9f),
                Units.P(0, -UsableH / 2 + 168), 960);
            _guideLater = label.transform;
        }

        bool GuideLaterHit(Vector2 pt)
        {
            if (_guideLater == null) return false;
            var c = (Vector2)_guideLater.position * Units.PointsPerUnit;
            return Mathf.Abs(pt.x - c.x) <= 90 && Mathf.Abs(pt.y - c.y) <= 24;
        }

        void DismissGuideAsLater()
        {
            _purchaseGuide?.Dismiss();
            _purchaseGuide = null;
            _guided = false;
            if (_guideLater != null) RenderShared.Destroy(_guideLater.gameObject);
            _guideLater = null;
            Scroll.Locked = false;
            S.Economy.SnoozeShopGuide();
            App.FlushSave();
            S.Analytics.ShopGuide("later");
        }

        /// Empile les sections dans le contenu défilant (0 = haut, y négatif vers le bas).
        void BuildContent()
        {
            _sectionTops.Clear();
            var content = Scroll.Content;
            float cursor = -Pt(16);
            float left = -_cw / 2;

            void Header(string key, string title, string trailing)
            {
                _sectionTops[key] = cursor + Pt(8);
                cursor -= Pt(16);
                Design.Text(content, title, NunitoWeight.Black, 20, Design.Ink, new Vector2(left, cursor), Z, HorizontalAlignmentOptions.Left);
                if (trailing != null)
                    Design.Text(content, trailing, NunitoWeight.ExtraBold, 14, Design.Ink2, new Vector2(-left, cursor), Z, HorizontalAlignmentOptions.Right);
                cursor -= Pt(16 + 12);
            }

            // « Sans pub » en bandeau, tout en haut.
            AddNoAdsCard(ref cursor);
            cursor -= Pt(24);

            var themes = ThemeCatalog.All;
            Header("themes", Loc.T("shop.tab_themes"), themes.Count(t => S.Themes.Owns(t.Id)) + " / " + themes.Count);
            float cardW = (_cw - Pt(10)) / 2, cardH = Pt(172);
            for (int i = 0; i < themes.Count; i++)
                AddThemeCard(themes[i], new Vector2(left + cardW / 2 + (i % 2) * (cardW + Pt(10)), cursor - cardH / 2 - (i / 2) * (cardH + Pt(10))),
                    new Vector2(cardW, cardH));
            cursor -= (themes.Count + 1) / 2 * (cardH + Pt(10)) - Pt(10) + Pt(28);

            Header("bubbles", Loc.T("shop.tab_bubbles"), null);
            AddCosmeticList(CosmeticCatalog.BubbleStyles, ref cursor);
            cursor -= Pt(28);

            Header("trails", Loc.T("shop.tab_trails"), null);
            AddCosmeticList(CosmeticCatalog.Trails, ref cursor);
            cursor -= Pt(28);

            Header("boosters", Loc.T("shop.tab_boosters"), null);
            AddBoosterList(ref cursor);
            cursor -= Pt(28);

            Header("coins", Loc.T("shop.tab_coins"), null);
            var packs = S.CoinStore.Packs;
            if (packs.Count == 0)
            {
                // Pas de produits : on tente le chargement (celui du lancement a pu échouer).
                if (!_attemptedProductLoad) ReloadProducts(false);
                string msg = Loc.T(_isLoadingProducts ? "shop.coins_loading" : "shop.coins_unavailable");
                Design.Text(content, msg, NunitoWeight.Bold, 15, Design.Ink2, new Vector2(0, cursor - Pt(16)), Z).ShrinkToFit(_cw);
                cursor -= Pt(40);
                if (!_isLoadingProducts)
                {
                    var retry = Design.Button("productsRetry", content, new Vector2(0, cursor - Pt(22)), Pt(200), Loc.T("shop.retry"), false, Z,
                        VectorIconName.LineRestart, 44);
                    AddTarget("productsRetry", retry, new Vector2(Pt(200), Pt(44)), () => { HapticManager.Light(); ReloadProducts(true); }, haptic: false);
                    cursor -= Pt(56);
                }
            }
            else
            {
                float pw = (_cw - Pt(10)) / 2, ph = Pt(132);
                for (int i = 0; i < packs.Count; i++)
                    AddCoinPackCard(packs[i], i, new Vector2(left + pw / 2 + (i % 2) * (pw + Pt(10)), cursor - ph / 2 - (i / 2) * (ph + Pt(10))),
                        new Vector2(pw, ph));
                cursor -= (packs.Count + 1) / 2 * (ph + Pt(10)) - Pt(10);
            }

            Scroll.ContentHeight = -cursor + Pt(24);
        }

        void ReloadProducts(bool rebuildFirst)
        {
            if (_isLoadingProducts) return;
            _attemptedProductLoad = true;
            _isLoadingProducts = true;
            if (rebuildFirst) Rebuild();   // affiche « Chargement… »
            S.CoinStore.LoadProducts(_ =>
            {
                _isLoadingProducts = false;
                if (Root != null) Rebuild();
            });
        }

        // MARK: - Briques

        /// Prix en pièces : capsule creusée, chiffre grisé si le solde ne suffit pas.
        Transform PricePill(Transform parent, Vector2 pos, int price, bool large = true, string prefix = null)
        {
            bool affordable = S.Economy.Balance >= price;
            var node = RenderShared.Node("Price", parent, Units.P(pos.x, pos.y));
            var label = Design.Text(node, (prefix ?? "") + price, NunitoWeight.Black, large ? 16 : 14, affordable ? Design.Ink : Design.Ink3,
                Vector2.zero, Z + 4, HorizontalAlignmentOptions.Left);
            float coin = large ? 20 : 16;
            float content = Pt(coin + 6) + label.WidthPt();
            float w = content + Pt(large ? 28 : 22), h = Pt(large ? 40 : 32);
            ShapeView.Rect("Bg", node, Units.P(-w / 2, 0), new Vector2(w, h), h / 2, Design.Sunk, default, 0, Z + 2);
            var c = Design.Coin(node, new Vector2(-w / 2 - content / 2 + Pt(coin / 2), 0), coin, Z + 3);
            if (!affordable) FxAlpha.Of(c.gameObject).Alpha = 0.5f;
            label.transform.localPosition = Units.P(-w / 2 - content / 2 + Pt(coin + 6), 0);
            node.name = "PricePill";
            // Le nœud est ancré par son bord droit : `pos` = bord droit.
            return node;
        }

        static float PillWidth(Transform pill)
        {
            var bg = pill.Find("Bg")?.GetComponent<ShapeView>();
            return bg != null ? bg.SizePt.x : 0;
        }

        /// « ✓ Équipé » (bord droit en `rightX`).
        void Equipped(Transform parent, float rightX, float y)
        {
            var color = Look.Theme.IsDefault ? Design.SeedGreen : Design.Ink;
            var l = Design.Text(parent, Loc.T("ui.shop.equipped"), NunitoWeight.Black, 15, color, new Vector2(rightX, y), Z + 4,
                HorizontalAlignmentOptions.Right);
            VectorIcon.Create(parent, VectorIconName.LineCheck, Units.P(rightX - l.WidthPt() - Pt(12), y), Pt(16), color, Z + 4);
        }

        /// « Choisir » : déjà possédé, pas équipé.
        void Select(Transform parent, float rightX, float y)
        {
            var l = Design.Text(parent, Loc.T("shop.select"), NunitoWeight.ExtraBold, 14, Design.Ink, Vector2.zero, Z + 4);
            float w = l.WidthPt() + Pt(24), h = Pt(34);
            var pos = Units.P(rightX - w / 2, y);
            ShapeView.Rect("Select", parent, pos, new Vector2(w, h), h / 2, Color.clear, Design.Line, Pt(1.5f), Z + 3);
            l.transform.localPosition = pos;
        }

        // MARK: - Cartes

        /// « Sans pub » : bandeau blanc, prix du store dans une capsule accent.
        void AddNoAdsCard(ref float cursor)
        {
            bool purchased = S.AdFree.IsPurchased;
            float h = Pt(purchased ? 72 : 88);
            var card = RenderShared.Node("noAds", Scroll.Content, Units.P(0, cursor - h / 2));
            Design.Surface("Bg", card, Vector2.zero, new Vector2(_cw, h), Pt(Design.CardRadius), Z);
            float x = -_cw / 2 + Pt(16);
            Design.IconChip(card, new Vector2(x + Pt(22), 0), 44, 14, Design.Sunk, VectorIconName.LineNoads, Z + 1, Design.Ink, 24, 1f);

            float right = _cw / 2 - Pt(16);
            float statusW;
            if (purchased)
            {
                Equipped(card, right, 0);
                statusW = Pt(110);
            }
            else
            {
                // Prix du store uniquement ; « … » tant qu'il n'est pas chargé (achat alors inactif).
                var price = Design.Text(card, S.AdFreeStore.DisplayPrice ?? "…", NunitoWeight.Black, 16, Design.OnAccent, Vector2.zero, Z + 4);
                float pw = Mathf.Max(Pt(76), price.WidthPt() + Pt(28)), ph = Pt(44);
                var pos = Units.P(right - pw / 2, 0);
                ShapeView.Rect("Price", card, pos, new Vector2(pw, ph), ph / 2, Design.Accent, default, 0, Z + 2);
                price.transform.localPosition = pos;
                statusW = pw;
            }
            float textX = x + Pt(44 + 14);
            float textW = right - statusW - Pt(12) - textX;
            Design.Text(card, Loc.T("menu.remove_ads"), NunitoWeight.Black, 17, Design.Ink, new Vector2(textX, purchased ? Pt(9) : Pt(16)), Z + 3,
                HorizontalAlignmentOptions.Left).ShrinkToFit(textW);
            if (purchased)
                Design.Text(card, Loc.T("shop.no_ads_desc"), NunitoWeight.Bold, 13, Design.Ink2, new Vector2(textX, -Pt(11)), Z + 3,
                    HorizontalAlignmentOptions.Left).ShrinkToFit(textW);
            else
            {
                var sub = Design.Text(card, Loc.T("ui.shop.noads_sub"), NunitoWeight.Bold, 13, Design.Ink2, new Vector2(textX, -Pt(4)), Z + 3,
                    HorizontalAlignmentOptions.Left);
                sub.ShrinkToFit(textW);
                // Bonus de bienvenue : 500 pièces offertes avec l'achat.
                var bonus = Design.Text(card, "+" + StoreCatalog.AdFreeBonusCoins, NunitoWeight.Black, 13, Design.Ink2,
                    new Vector2(textX, -Pt(22)), Z + 3, HorizontalAlignmentOptions.Left);
                Design.Coin(card, new Vector2(textX + bonus.WidthPt() + Pt(11), -Pt(22)), 14, Z + 3);
            }
            AddTarget("noAds", card, new Vector2(_cw, h), HandleNoAds, animate: false, haptic: false);
            cursor -= h;
        }

        /// Thème : aperçu 4 × 2 sur le fond du thème, nom et état ; le thème équipé est cerclé d'encre.
        void AddThemeCard(Theme theme, Vector2 pos, Vector2 size)
        {
            bool isActive = S.Themes.ActiveId == theme.Id;
            bool owned = S.Themes.Owns(theme.Id);
            string name = "item:theme:" + theme.Id;
            var card = RenderShared.Node(name, Scroll.Content, Units.P(pos.x, pos.y));
            float r = Pt(Design.CardRadius);
            if (isActive)
                ShapeView.Rect("Ring", card, Vector2.zero, size + Design.Pt(5, 5), r + Pt(2.5f), Design.Ink, default, 0, Z - 1);
            Design.Surface("Bg", card, Vector2.zero, size, r, Z, hair: !isActive);

            // Aperçu : les huit premières bulles du thème, sur son fond (coins du haut arrondis).
            float previewH = Pt(112);
            float py = size.y / 2 - previewH / 2;
            var bgColor = Colors.FromRgb(theme.Background);
            ShapeView.Rect("PreviewTop", card, Units.P(0, py), new Vector2(size.x, previewH), r, bgColor, default, 0, Z + 1);
            ShapeView.Rect("PreviewFill", card, Units.P(0, py - previewH / 4), new Vector2(size.x, previewH / 2), 0, bgColor, default, 0, Z + 1);
            float cell = Mathf.Min((size.x - Pt(20)) / 4, (previewH - Pt(16)) / 2);
            float d = cell - Pt(6);
            var digit = Colors.FromRgb(theme.Digit);
            for (int i = 0; i < 8; i++)
            {
                var c = Units.P(-cell * 1.5f + (i % 4) * cell, py + cell / 2 - (i / 4) * cell);
                ShapeView.Circle("B", card, c, d / 2, Colors.FromRgb(theme.ColorForValue(i + 1)), default, 0, Z + 2);
                TenGoText.Create(card, (i + 1).ToString(), NunitoWeight.Bold, d * 0.5f, digit, c, Z + 3);
            }

            float rowY = -size.y / 2 + Pt(30);
            float lx = -size.x / 2 + Pt(14), rx = size.x / 2 - Pt(12);
            float statusW;
            if (isActive) { Equipped(card, rx, rowY); statusW = Pt(84); }
            else if (owned) { Select(card, rx, rowY); statusW = Pt(80); }
            else statusW = PillWidth(PricePill(card, new Vector2(rx, rowY), theme.Price));
            Design.Text(card, Loc.T(theme.NameKey), NunitoWeight.Black, 16, Design.Ink, new Vector2(lx, rowY), Z + 4, HorizontalAlignmentOptions.Left)
                .ShrinkToFit(rx - statusW - Pt(8) - lx);
            AddTarget(name, card, size, () => HandleTheme(theme, card), animate: false, haptic: false);
        }

        /// Matières ou tracés : une carte, une ligne par objet (aperçu, nom, état).
        void AddCosmeticList(IReadOnlyList<CosmeticStyle> styles, ref float cursor)
        {
            float rowH = Pt(64);
            var list = RenderShared.Node("List", Scroll.Content, Units.P(0, cursor - styles.Count * rowH / 2));
            Design.Surface("Bg", list, Vector2.zero, new Vector2(_cw, styles.Count * rowH), Pt(Design.CardRadius), Z);
            for (int i = 0; i < styles.Count; i++)
            {
                var style = styles[i];
                var kind = style.Kind;
                bool isActive = S.Cosmetics.ActiveId(kind) == style.Id;
                bool owned = S.Cosmetics.Owns(kind, style.Id);
                string prefix = kind == CosmeticKind.BubbleStyle ? "bubbleStyle" : "trail";
                string name = $"item:{prefix}:{style.Id}";
                var row = RenderShared.Node(name, list, Units.P(0, styles.Count * rowH / 2 - rowH * (i + 0.5f)));
                if (i > 0) ShapeView.Rect("Sep", row, Units.P(0, rowH / 2), new Vector2(_cw, Pt(1)), 0, Design.Line, default, 0, Z + 1);
                float x = -_cw / 2 + Pt(16);
                if (kind == CosmeticKind.BubbleStyle)
                    BubbleView.Create(row, 5, Units.P(x + Pt(18), 0), Z + 2, Look.ParseBubble(style.Id), Pt(18));
                else
                {
                    var pts = new List<Vector2>();
                    foreach (var (px, py) in new[] { (-16f, -6f), (-5f, 7f), (5f, -7f), (16f, 6f) }) pts.Add(Units.P(x + Pt(18) + Pt(px), Pt(py)));
                    Rendering.TrailRenderer.Make(row, pts, Look.ParseTrail(style.Id), Design.Accent, 0, Z + 2);
                }
                float rx = _cw / 2 - Pt(16);
                float statusW;
                if (isActive) { Equipped(row, rx, 0); statusW = Pt(90); }
                else if (owned) { Select(row, rx, 0); statusW = Pt(84); }
                else statusW = PillWidth(PricePill(row, new Vector2(rx, 0), style.Price));
                float textX = x + Pt(36 + 14);
                Design.Text(row, Loc.T(style.NameKey), NunitoWeight.ExtraBold, 16, Design.Ink, new Vector2(textX, 0), Z + 3,
                    HorizontalAlignmentOptions.Left).ShrinkToFit(rx - statusW - Pt(10) - textX);
                AddTarget(name, row, new Vector2(_cw, rowH), () => HandleCosmetic(style, row), animate: false, haptic: false);
            }
            cursor -= styles.Count * rowH;
        }

        /// Boosters : icône, nom, stock ; à droite le lot remisé puis l'unité (celle du tutoriel).
        void AddBoosterList(ref float cursor)
        {
            var all = Boosters.All;
            float rowH = Pt(68);
            var list = RenderShared.Node("Boosters", Scroll.Content, Units.P(0, cursor - all.Count * rowH / 2));
            Design.Surface("Bg", list, Vector2.zero, new Vector2(_cw, all.Count * rowH), Pt(Design.CardRadius), Z);
            for (int i = 0; i < all.Count; i++)
            {
                var booster = all[i];
                var row = RenderShared.Node("booster:" + booster.Id(), list, Units.P(0, all.Count * rowH / 2 - rowH * (i + 0.5f)));
                if (i > 0) ShapeView.Rect("Sep", row, Units.P(0, rowH / 2), new Vector2(_cw, Pt(1)), 0, Design.Line, default, 0, Z + 1);
                float x = -_cw / 2 + Pt(16);
                Design.IconChip(row, new Vector2(x + Pt(20), 0), 40, 13, Design.Sunk, IconOf(booster), Z + 1, Design.Ink, 22, 1f);
                AddTarget("booster:" + booster.Id(), row, new Vector2(_cw, rowH), () => HandleBoosterPurchase(booster, 1), animate: false, haptic: false);

                // Deux achats : l'unité (celle du tutoriel) au bord, le lot remisé à sa gauche.
                float rx = _cw / 2 - Pt(14);
                var unit = PricePill(row, new Vector2(rx, 0), booster.Price());
                float unitW = PillWidth(unit);
                unit.name = $"booster:{booster.Id()}:1";
                AddTarget(unit.name, unit, new Vector2(unitW, Pt(40)), () => HandleBoosterPurchase(booster, 1), animate: false, haptic: false,
                    offsetPt: new Vector2(-unitW / 2, 0));
                int q = booster.BundleQuantity();
                var bundle = PricePill(row, new Vector2(rx - unitW - Pt(6), 0), booster.BundlePrice(), false, "×" + q + "  ");
                float bundleW = PillWidth(bundle);
                bundle.name = $"booster:{booster.Id()}:{q}";
                AddTarget(bundle.name, bundle, new Vector2(bundleW, Pt(36)), () => HandleBoosterPurchase(booster, q), animate: false, haptic: false,
                    offsetPt: new Vector2(-bundleW / 2, 0));

                float textX = x + Pt(40 + 14);
                float textW = rx - unitW - Pt(6) - bundleW - Pt(10) - textX;
                Design.Text(row, Loc.T(booster.TitleKey()), NunitoWeight.ExtraBold, 16, Design.Ink, new Vector2(textX, Pt(9)), Z + 3,
                    HorizontalAlignmentOptions.Left).ShrinkToFit(textW);
                string stock = Loc.F("shop.booster_stock", S.Economy.BoosterCount(booster));
                if (stock.Length > 0) stock = char.ToUpper(stock[0]) + stock.Substring(1);
                Design.Text(row, stock, NunitoWeight.Bold, 13, Design.Ink2, new Vector2(textX, -Pt(11)), Z + 3, HorizontalAlignmentOptions.Left)
                    .ShrinkToFit(textW);
            }
            cursor -= all.Count * rowH;
        }

        static VectorIconName IconOf(Booster b) => b switch
        {
            Booster.Hint => VectorIconName.LineBulb,
            Booster.Shuffle => VectorIconName.LineShuffle,
            _ => VectorIconName.LineHammer,
        };

        /// Pack de pièces (achat en vraie monnaie) : pile de pièces, montant, prix du store.
        void AddCoinPackCard(CoinPack pack, int index, Vector2 pos, Vector2 size)
        {
            var card = RenderShared.Node("pack:" + pack.ProductId, Scroll.Content, Units.P(pos.x, pos.y));
            Design.Surface("Bg", card, Vector2.zero, size, Pt(Design.CardRadius), Z);
            int coins = Mathf.Clamp(index + 1, 1, 3);
            float iy = size.y / 2 - Pt(34);
            for (int c = 0; c < coins; c++)
                Design.Coin(card, new Vector2((c - (coins - 1) / 2f) * Pt(18), iy), 36, Z + 2 + c);
            Design.Text(card, "+" + Design.Number(pack.Coins), NunitoWeight.Black, 18, Design.Ink, new Vector2(0, Pt(2)), Z + 3).ShrinkToFit(size.x - Pt(16));
            var price = Design.Text(card, pack.DisplayPrice, NunitoWeight.Black, 15, Design.OnAccent, Vector2.zero, Z + 4);
            float pw = Mathf.Min(size.x - Pt(24), Mathf.Max(Pt(84), price.WidthPt() + Pt(28))), ph = Pt(36);
            var pp = Units.P(0, -size.y / 2 + Pt(30));
            ShapeView.Rect("Price", card, pp, new Vector2(pw, ph), ph / 2, Design.Accent, default, 0, Z + 2);
            price.transform.localPosition = pp;
            price.ShrinkToFit(pw - Pt(12));
            AddTarget("pack:" + pack.ProductId, card, size, () => HandleCoinPack(pack), animate: false, haptic: false);
        }

        /// Solde : capsule blanche ancrée par son bord droit ; renvoie sa largeur.
        float AddBalanceChip(float rightX, float y)
        {
            var container = RenderShared.Node("balanceChip", Root);
            float height = Pt(44);
            var number = Design.Text(container, S.Economy.Balance.ToString(), NunitoWeight.Black, 17, Design.Ink, Vector2.zero, Z + 13,
                HorizontalAlignmentOptions.Left);
            float width = Pt(10 + 20 + 8) + number.WidthPt() + Pt(14);
            Design.Surface("Bg", container, Vector2.zero, new Vector2(width, height), height / 2, Z + 11);
            Design.Coin(container, new Vector2(-width / 2 + Pt(20), 0), 20, Z + 12);
            number.transform.localPosition = Units.P(-width / 2 + Pt(38), 0);
            container.localPosition = Units.P(rightX - width / 2, y);
            _balanceChipCenter = Units.ToPoints(1) * (Vector2)container.localPosition;
            return width;
        }

        // MARK: - Actions

        void HandleTheme(Theme theme, Transform card)
        {
            if (S.Themes.ActiveId == theme.Id) return;
            if (S.Themes.Owns(theme.Id)) { S.Themes.SetActive(theme.Id); HapticManager.Light(); }
            else if (S.Themes.Purchase(theme)) { S.Themes.SetActive(theme.Id); HapticManager.Medium(); }
            else { Shake(card); HapticManager.Light(); return; }
            App.FlushSave();
            S.ApplyLook();
            Rebuild();
        }

        void HandleCosmetic(CosmeticStyle style, Transform card)
        {
            var kind = style.Kind;
            if (S.Cosmetics.ActiveId(kind) == style.Id) return;
            if (S.Cosmetics.Owns(kind, style.Id)) { S.Cosmetics.SetActive(kind, style.Id); HapticManager.Light(); }
            else if (S.Cosmetics.Purchase(style)) { S.Cosmetics.SetActive(kind, style.Id); HapticManager.Medium(); }
            else { Shake(card); HapticManager.Light(); return; }
            App.FlushSave();
            S.ApplyLook();
            Rebuild();
        }

        /// `quantity` = 1 pour l'unité, `BundleQuantity` pour le lot remisé.
        void HandleBoosterPurchase(Booster booster, int quantity)
        {
            bool isBundle = quantity == booster.BundleQuantity() && quantity > 1;
            int cost = isBundle ? booster.BundlePrice() : booster.Price() * Mathf.Max(1, quantity);
            bool ok = isBundle ? S.Economy.PurchaseBundle(booster) : S.Economy.PurchaseBooster(booster, Mathf.Max(1, quantity));
            if (!ok)
            {
                Shake(FindTarget("booster:" + booster.Id())?.Node);
                HapticManager.Light();
                return;
            }
            S.Analytics.BoosterPurchased(booster.Id(), quantity, cost, "shop");
            HapticManager.Medium();
            App.FlushSave();
            Rebuild();
        }

        /// Achat depuis le coach-mark : UNE unité d'indice, remboursée 1,4 s plus tard.
        void CompleteGuidedPurchase()
        {
            var hint = Booster.Hint;
            if (!S.Economy.PurchaseBooster(hint, 1))
            {
                Shake(FindTarget("booster:" + hint.Id())?.Node);
                return;
            }
            S.Analytics.BoosterPurchased(hint.Id(), 1, hint.Price(), "tutorial");
            S.Analytics.ShopGuide("purchased");
            _guided = false;
            _purchaseGuide?.Dismiss();
            _purchaseGuide = null;
            Scroll.Locked = false;
            HapticManager.Medium();
            App.FlushSave();
            Rebuild();
            ShowToast(Loc.T("guide.shop_success"));

            After(1.4f, () =>
            {
                S.Economy.AddCoins(hint.Price());
                S.Economy.MarkShopGuideCompleted();
                S.Analytics.BoosterRefunded(hint.Id(), hint.Price());
                S.Analytics.ShopGuide("refunded");
                HapticManager.Light();
                App.FlushSave();
                Rebuild();
                ShowToast(Loc.T("guide.shop_refund"));
            });
        }

        void HandleNoAds()
        {
            if (S.AdFree.IsPurchased) return;
            if (S.AdFreeStore.DisplayPrice == null)
            {
                // Prix inconnu : pas d'achat, on relance le chargement.
                Shake(FindTarget("noAds")?.Node);
                S.AdFreeStore.LoadProducts();
                return;
            }
            HapticManager.Light();
            S.AdFreeStore.PurchaseAdFree("shop", (outcome, message) =>
            {
                if (Root == null) return;
                App.FlushSave();
                Rebuild();
                if (outcome == PurchaseOutcome.Success)
                {
                    HapticManager.Medium();
                    ShowBonusFeedback(StoreCatalog.AdFreeBonusCoins);
                    ShowToast(Loc.F("shop.purchase_success_noads", StoreCatalog.AdFreeBonusCoins));
                }
                else
                {
                    if (outcome == PurchaseOutcome.Failed) Shake(FindTarget("noAds")?.Node);
                    if (!string.IsNullOrEmpty(message)) ShowToast(message);
                }
            });
        }

        void HandleCoinPack(CoinPack pack)
        {
            HapticManager.Light();
            S.CoinStore.Purchase(pack, "shop", (outcome, message) =>
            {
                if (Root == null) return;
                if (outcome == PurchaseOutcome.Success)
                {
                    HapticManager.Medium();
                    App.FlushSave();
                    Rebuild();
                    ShowToast(Loc.F("shop.purchase_success_coins", pack.Coins));
                }
                else if (!string.IsNullOrEmpty(message)) ShowToast(message);
            });
        }

        /// « +500 » doré qui s'élève depuis le solde.
        void ShowBonusFeedback(int amount)
        {
            var popup = TenGoText.Create(Root, "+" + amount, NunitoWeight.Black, Pt(20), Design.GoldRim,
                Units.P(_balanceChipCenter.x, _balanceChipCenter.y - Pt(30)), 300);
            var go = popup.gameObject;
            FxAlpha.Of(go);
            Tw.Run(go, Tw.Seq(Tw.Par(Tw.MoveBy(popup.transform, Units.P(0, 52), 0.8f),
                Tw.Seq(Tw.Wait(0.4f), Tw.FadeOut(go, 0.4f))), Tw.Remove(go)));
        }

        /// Message temporaire au-dessus du pied d'écran : capsule d'encre.
        void ShowToast(string text)
        {
            LastToast = text;
            if (_toast != null) RenderShared.Destroy(_toast.gameObject);
            var node = RenderShared.Node("Toast", Root, Units.P(0, _viewportBottom + Pt(40)));
            var go = node.gameObject;
            FxAlpha.Of(go).Alpha = 0;
            float maxW = _cw - Pt(32);
            var label = Design.Text(node, text, NunitoWeight.ExtraBold, 15, Design.Bg, Vector2.zero, 402).Wrap(maxW);
            ShapeView.Rect("Bg", node, Vector2.zero, new Vector2(Mathf.Min(maxW, label.WidthPt()) + Pt(32), label.HeightPt() + Pt(22)), Pt(16),
                Design.Ink, default, 0, 400);
            _toast = node;
            Tw.Run(go, Tw.Seq(Tw.FadeIn(go, 0.2f), Tw.Wait(2.2f), Tw.FadeOut(go, 0.4f), Tw.Remove(go)));
        }

        void GoBack()
        {
            if (_returnToGame)
            {
                // Reprise sans rejouer les effets de début de partie.
                var saved = S.Games.Load();
                App.StartGame(saved != null ? GameLaunch.Normal(saved, resuming: true) : GameLaunch.Normal());
            }
            else App.ShowMenu();
        }

        // MARK: - Toucher (guide d'achat)

        protected override bool InterceptDown(Vector2 pt)
        {
            if (_purchaseGuide == null) return false;
            _guideTouch = true;   // traité au relâchement, comme iOS
            return true;
        }

        public override void PointerUp(Vector2 pt)
        {
            if (!_guideTouch) { base.PointerUp(pt); return; }
            _guideTouch = false;
            if (_purchaseGuide == null) return;
            if (GuideLaterHit(pt)) { DismissGuideAsLater(); return; }
            // Le bouton de retour reste actif pendant le guide.
            var back = FindTarget("shopBack");
            if (back != null && back.Contains(pt)) { DismissGuideAsLater(); GoBack(); return; }
            if (TabBarNode != null && TabBar.TabAt(TabBarNode, pt) is Tab tab && tab != Tab.Shop)
            {
                DismissGuideAsLater();
                OnTab(tab);
                return;
            }
            switch (_purchaseGuide.HandleTouch(pt))
            {
                case CoachMarkOverlay.TouchResult.Ignored: return;
                case CoachMarkOverlay.TouchResult.Target:
                    if (_guideLater != null) RenderShared.Destroy(_guideLater.gameObject);
                    _guideLater = null;
                    _purchaseGuide = null;
                    CompleteGuidedPurchase();
                    return;
                default:
                    _purchaseGuide = null;
                    _guided = false;
                    Scroll.Locked = false;
                    if (_guideLater != null) RenderShared.Destroy(_guideLater.gameObject);
                    _guideLater = null;
                    return;
            }
        }

        /// Tap du guide (tests) : « later » ou « target ».
        public void GuideTap(bool target)
        {
            if (_purchaseGuide == null) return;
            if (!target) { DismissGuideAsLater(); return; }
            var f = _purchaseGuide.TargetFrame;
            InterceptDown(f.center);
            PointerUp(f.center);
        }

        public override void Back()
        {
            if (_purchaseGuide != null) DismissGuideAsLater();
            GoBack();
        }
    }
}
