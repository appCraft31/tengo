using System;
using System.Collections.Generic;
using TenGo.App;
using TenGo.Core;
using TenGo.Fx;
using TenGo.Game;
using TenGo.Haptics;
using TenGo.Rendering;
using TenGo.Services;
using TMPro;
using UnityEngine;

namespace TenGo.Screens
{
    /// Boutique (BoutiqueScene.swift) : une seule page défilante, classée par
    /// sections (« Sans pub », Thèmes, Matières, Tracés, Boosters, Pièces).
    /// Achat avec les pièces gagnées, sélection du cosmétique actif ; les achats
    /// en vraie monnaie passent par `IAdFreeStore` / `ICoinStore` (simulés
    /// jusqu'à l'étape 7). Ouverte depuis une partie, un bouton de retour
    /// remplace les onglets. Mode guidé : premier achat d'un indice, remboursé.
    public sealed class ShopScreen : ScreenBase
    {
        const int Z = 20;
        const float CardH = 150;
        static readonly Color GoldInk = new(0.45f, 0.34f, 0.10f, 1f);

        readonly bool _returnToGame;
        bool _guided;
        float _cardW = 280;
        float _viewportBottom;
        readonly Dictionary<string, float> _sectionTops = new();
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
            _purchaseGuide = null;
            _guideLater = null;
            _toast = null;
            TabBarNode = null;

            var theme = Look.Theme;
            App.SetBackground(Look.Background);
            ThemeBackground.Make(Root, theme, SceneSize);

            float usableW = UsableW;
            _cardW = usableW / 2 - 16;
            float topY = HalfH - TopInset - 26;
            float bottomY = -HalfH;

            var title = TenGoText.Create(Root, Loc.T("shop.title"), Fonts.AvenirHeavy, 38, Look.Logo,
                Units.P(-usableW / 2 + 24, topY - 14), Z + 10, horizontal: HorizontalAlignmentOptions.Left);
            float chipW = AddBalanceChip(topY - 14);
            title.ShrinkToFit(usableW - 48 - chipW - 16);

            // Ouverte depuis l'accueil, la boutique EST l'onglet Boutique ; depuis
            // une partie, elle garde son bouton de retour (les onglets abandonneraient la grille).
            float bottomChromeH;
            if (!_returnToGame)
            {
                AddTabBar(Tab.Shop);
                bottomChromeH = BottomInset + TabBar.Height;
            }
            else
            {
                AddBackButton(bottomY + 64);
                bottomChromeH = 88;
            }

            float viewportTop = topY - 56;
            _viewportBottom = bottomY + bottomChromeH + 16;
            Scroll = new ScrollArea(Root, viewportTop, _viewportBottom, -usableW / 2, usableW / 2);
            BuildContent();
            Scroll.ApplyClip();
            Scroll.SetOffset(previousOffset);

            if (_guided) AttachGuide();
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
            var label = TenGoText.Create(Root, Loc.T("guide.shop_later"), Fonts.AvenirDemiBold, 16, Colors.White(0.98f, 0.9f),
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
            float cursor = 0;
            float colCenter = UsableW / 4;
            float[] colX = { -colCenter, colCenter };
            const float rowGap = 18, sectionGap = 34;

            void Header(string text, string key)
            {
                _sectionTops[key] = cursor;
                cursor -= 26;
                TenGoText.Create(content, text, Fonts.AvenirBold, 24, Look.Logo, Units.P(-UsableW / 2 + 22, cursor), Z,
                    horizontal: HorizontalAlignmentOptions.Left);
                cursor -= 30;
            }
            Vector2 GridPlace(int index) => new(colX[index % 2], cursor - CardH / 2 - index / 2 * (CardH + rowGap));
            void EndGrid(int count) => cursor -= (count + 1) / 2 * (CardH + rowGap) + sectionGap;

            // « Sans pub » mis en avant, tout en haut.
            AddNoAdsCard(ref cursor);

            Header(Loc.T("shop.tab_themes"), "themes");
            for (int i = 0; i < ThemeCatalog.All.Count; i++) AddThemeCard(ThemeCatalog.All[i], GridPlace(i));
            EndGrid(ThemeCatalog.All.Count);

            Header(Loc.T("shop.tab_bubbles"), "bubbles");
            var styles = CosmeticCatalog.BubbleStyles;
            for (int i = 0; i < styles.Count; i++)
            {
                var s = styles[i];
                var preview = RenderShared.Node("Preview", null);
                BubbleView.Create(preview, 5, Vector2.zero, Z + 2, Look.ParseBubble(s.Id), 23);
                AddCosmeticCard(s, preview, 36, GridPlace(i));
            }
            EndGrid(styles.Count);

            Header(Loc.T("shop.tab_trails"), "trails");
            var trails = CosmeticCatalog.Trails;
            for (int i = 0; i < trails.Count; i++)
            {
                var t = trails[i];
                AddCosmeticCard(t, TrailPreview(t), 36, GridPlace(i));
            }
            EndGrid(trails.Count);

            Header(Loc.T("shop.tab_boosters"), "boosters");
            // Rangées pleine largeur : elles portent une description.
            foreach (var b in Boosters.All) AddBoosterRow(b, ref cursor);
            cursor -= sectionGap - 16;

            Header(Loc.T("shop.tab_coins"), "coins");
            var packs = S.CoinStore.Packs;
            if (packs.Count == 0)
            {
                // Pas de produits : on tente le chargement (celui du lancement a pu échouer).
                if (!_attemptedProductLoad) ReloadProducts(false);
                string msg = Loc.T(_isLoadingProducts ? "shop.coins_loading" : "shop.coins_unavailable");
                TenGoText.Create(content, msg, Fonts.AvenirMedium, 19, Look.Logo.WithAlpha(0.7f), Units.P(0, cursor - 30), Z)
                    .ShrinkToFit(UsableW - 40);
                cursor -= 80;
                if (!_isLoadingProducts)
                {
                    var retry = RenderShared.Node("productsRetry", content, Units.P(0, cursor + 8));
                    var pill = ShapeView.Rect("Pill", retry, Vector2.zero, new Vector2(220, 44), 22, Look.Logo.WithAlpha(0.06f),
                        Look.Logo.WithAlpha(0.18f), 1, Z);
                    Relief.Raise(pill, 3);
                    TenGoText.Create(retry, Loc.T("shop.retry"), Fonts.AvenirDemiBold, 17, Look.Logo, Vector2.zero, Z + 2);
                    AddTarget("productsRetry", retry, new Vector2(220, 44), () => { HapticManager.Light(); ReloadProducts(true); }, haptic: false);
                    cursor -= 50;
                }
            }
            else
            {
                for (int i = 0; i < packs.Count; i++) AddCoinPackCard(packs[i], GridPlace(i));
                EndGrid(packs.Count);
            }

            Scroll.ContentHeight = -cursor + 8;
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

        // MARK: - Cartes

        /// Carte « Sans pub » : fond accentué et halo pulsant tant que non achetée.
        void AddNoAdsCard(ref float cursor)
        {
            var theme = Look.Theme;
            bool purchased = S.AdFree.IsPurchased;
            float h = purchased ? 116 : 132;
            float w = UsableW - 32;
            var accent = Look.Accent;
            var ink = accent.ReadableInk();
            var card = RenderShared.Node("noAds", Scroll.Content, Units.P(0, cursor - h / 2));

            if (!purchased)
            {
                var halo = ShapeView.Rect("Halo", card, Vector2.zero, new Vector2(w + 12, h + 12), 32, accent.WithAlpha(0.35f), default, 0, Z - 2);
                var hgo = halo.gameObject;
                FxAlpha.Of(hgo);
                Tw.Run(hgo, Tw.Forever(Tw.Seq(
                    Tw.Par(Tw.Scale(halo.transform, 1.03f, 1.1f), Tw.Fade(hgo, 0.5f, 1.1f)),
                    Tw.Par(Tw.Scale(halo.transform, 1f, 1.1f), Tw.Fade(hgo, 1f, 1.1f)))));
            }
            ShapeView.Rect("Shadow", card, Units.P(0, -5), new Vector2(w, h), 26, Colors.White(0, 0.08f), default, 0, Z - 1);
            var bg = ShapeView.Rect("Bg", card, Vector2.zero, new Vector2(w, h), 26, accent, Look.Logo.WithAlpha(0.25f), purchased ? 3 : 1.5f, Z);
            Relief.Raise(bg, 6);

            // Pictogramme « écran publicitaire barré ».
            var picto = Units.P(-w / 2 + 48, 0);
            Relief.IconChip(card, picto, 32, accent, Z + 1);
            ShapeView.Rect("Screen", card, picto, new Vector2(34, 24), 5, Color.clear, ink.WithAlpha(0.85f), 2.5f, Z + 2);
            StrokeView.Create("Slash", card, new[] { picto + Units.P(-19, -14), picto + Units.P(19, 14) }, ink.WithAlpha(0.85f), 3, Z + 3);

            float textX = -w / 2 + 86;
            float priceW = purchased ? 96 : 110;
            float textMax = w - 86 - priceW - 40;
            TenGoText.Create(card, Loc.T("menu.remove_ads"), Fonts.AvenirBold, 23, ink, Units.P(textX, purchased ? 12 : 22), Z + 2,
                horizontal: HorizontalAlignmentOptions.Left).ShrinkToFit(textMax);
            TenGoText.Create(card, Loc.T("shop.no_ads_desc"), Fonts.AvenirMedium, 14, ink.WithAlpha(0.75f), Units.P(textX, purchased ? -14 : -2), Z + 2,
                horizontal: HorizontalAlignmentOptions.Left).ShrinkToFit(textMax);

            if (!purchased)
            {
                // Bonus de bienvenue : 500 pièces offertes avec l'achat.
                CoinIcon.Make(card, Units.P(-w / 2 + 94, -26), 8, Z + 2);
                TenGoText.Create(card, Loc.T("shop.no_ads_bonus"), Fonts.AvenirBold, 14, GoldInk, Units.P(-w / 2 + 108, -26), Z + 2,
                    horizontal: HorizontalAlignmentOptions.Left).ShrinkToFit(textMax - 22);
            }

            var pillPos = Units.P(w / 2 - (purchased ? 70 : 74), 0);
            var pill = ShapeView.Rect("Price", card, pillPos, new Vector2(priceW, 36), 18, Look.Background, default, 0, Z + 1);
            if (!purchased) Relief.Raise(pill, 3);
            // Prix du store uniquement ; « … » tant qu'il n'est pas chargé (achat alors inactif).
            string priceText = purchased ? Loc.T("shop.active") : S.AdFreeStore.DisplayPrice ?? "…";
            TenGoText.Create(card, priceText, Fonts.AvenirBold, purchased ? 15 : 16, Look.Logo, pillPos, Z + 3).ShrinkToFit(priceW - 12);

            AddTarget("noAds", card, new Vector2(w, h), HandleNoAds, animate: false, haptic: false);
            cursor -= h + 18;
        }

        /// Rangée d'un booster : icône, nom, description, stock, prix unité / lot.
        void AddBoosterRow(Booster booster, ref float cursor)
        {
            const float h = 112;
            float w = UsableW - 32;
            var logo = Look.Logo;
            var card = RenderShared.Node("booster:" + booster.Id(), Scroll.Content, Units.P(0, cursor - h / 2));
            ShapeView.Rect("Shadow", card, Units.P(0, -5), new Vector2(w, h), 26, Colors.White(0, 0.08f), default, 0, Z - 1);
            var bg = ShapeView.Rect("Bg", card, Vector2.zero, new Vector2(w, h), 26, new Color(0.99f, 0.97f, 0.94f), logo.WithAlpha(0.18f), 1.5f, Z);
            Relief.Raise(bg, 5);

            int index = 0;
            for (int i = 0; i < Boosters.All.Count; i++) if (Boosters.All[i] == booster) index = i;
            var chipPos = Units.P(-w / 2 + 48, 0);
            ShapeView.Circle("Chip", card, chipPos, 30, Look.BubbleColor(new[] { 3, 5, 7 }[index % 3]).MixedWithWhite(0.35f), default, 0, Z + 1);
            VectorIcon.Create(card, IconOf(booster), chipPos, 36, Colors.White(0.22f), Z + 2);

            float textX = -w / 2 + 86;
            float textWidth = w - 86 - 176;
            TenGoText.Create(card, Loc.T(booster.TitleKey()), Fonts.AvenirBold, 22, logo, Units.P(textX, 30), Z + 2,
                horizontal: HorizontalAlignmentOptions.Left).ShrinkToFit(textWidth);
            var desc = TenGoText.Create(card, Loc.T(booster.DetailsKey()), Fonts.AvenirMedium, 14, Colors.White(0.32f), Units.P(textX, 2), Z + 2,
                horizontal: HorizontalAlignmentOptions.Left);
            desc.Wrap(textWidth);
            TenGoText.Create(card, Loc.F("shop.booster_stock", S.Economy.BoosterCount(booster)), Fonts.AvenirMedium, 13, Colors.White(0.45f),
                Units.P(textX, -34), Z + 2, horizontal: HorizontalAlignmentOptions.Left).ShrinkToFit(textWidth);

            AddTarget("booster:" + booster.Id(), card, new Vector2(w, h), () => HandleBoosterPurchase(booster, 1), animate: false, haptic: false);
            // Deux achats : l'unité (celle du tutoriel) et le lot remisé.
            AddBoosterPricePill(card, booster, 1, booster.Price(), new Vector2(w / 2 - 74, 24));
            AddBoosterPricePill(card, booster, booster.BundleQuantity(), booster.BundlePrice(), new Vector2(w / 2 - 74, -24));
            cursor -= h + 16;
        }

        void AddBoosterPricePill(Transform card, Booster booster, int quantity, int price, Vector2 pos)
        {
            string name = $"booster:{booster.Id()}:{quantity}";
            bool affordable = S.Economy.Balance >= price;
            var node = RenderShared.Node(name, card, Units.P(pos.x, pos.y));
            var accent = Look.Accent;
            var pill = ShapeView.Rect("Pill", node, Vector2.zero, new Vector2(124, 38), 19, affordable ? accent : Colors.White(0.90f), default, 0, Z + 1);
            Relief.Raise(pill, 3);
            var ink = affordable ? accent.ReadableInk() : Colors.White(0.45f);
            TenGoText.Create(node, "×" + quantity, Fonts.AvenirBold, 15, ink, Units.P(-46, 0), Z + 3, horizontal: HorizontalAlignmentOptions.Left);
            CoinIcon.Make(node, Units.P(2, 0), 8, Z + 3);
            TenGoText.Create(node, price.ToString(), Fonts.AvenirBold, 16, ink, Units.P(14, 0), Z + 3, horizontal: HorizontalAlignmentOptions.Left);
            AddTarget(name, node, new Vector2(124, 38), () => HandleBoosterPurchase(booster, quantity), animate: false, haptic: false);
        }

        static VectorIconName IconOf(Booster b) => b switch
        {
            Booster.Hint => VectorIconName.BoosterHint,
            Booster.Shuffle => VectorIconName.BoosterShuffle,
            _ => VectorIconName.BoosterHammer,
        };

        /// Pack de pièces (achat en vraie monnaie).
        void AddCoinPackCard(CoinPack pack, Vector2 pos)
        {
            var preview = RenderShared.Node("Preview", null);
            CoinIcon.Make(preview, Vector2.zero, 24, Z + 2);
            var card = AddCardFrame("pack:" + pack.ProductId, Look.Background, Look.Logo.WithAlpha(0.18f), 1, pack.Coins.ToString(), Look.Logo,
                preview, 40, pos);
            var accent = Look.Accent;
            var pill = ShapeView.Rect("Pill", card, Units.P(0, -48), new Vector2(110, 34), 17, accent, default, 0, Z + 1);
            Relief.Raise(pill, 3);
            TenGoText.Create(card, pack.DisplayPrice, Fonts.AvenirBold, 16, accent.ReadableInk(), Units.P(0, -48), Z + 3).ShrinkToFit(100);
            AddTarget("pack:" + pack.ProductId, card, new Vector2(_cardW, CardH), () => HandleCoinPack(pack), animate: false, haptic: false);
        }

        void AddThemeCard(Theme theme, Vector2 pos)
        {
            bool isActive = S.Themes.ActiveId == theme.Id;
            bool owned = S.Themes.Owns(theme.Id);
            // Aperçu : cinq bulles de la palette.
            var preview = RenderShared.Node("Preview", null);
            int[] values = { 1, 3, 5, 7, 9 };
            float spacing = Mathf.Min(42, (_cardW - 56) / (values.Length - 1));
            float totalW = (values.Length - 1) * spacing;
            for (int i = 0; i < values.Length; i++)
                ShapeView.Circle("Dot", preview, Units.P(-totalW / 2 + i * spacing, 0), 16, Colors.FromRgb(theme.ColorForValue(values[i])),
                    Colors.White(1, 0.22f), 1, Z + 2);
            var accent = Colors.FromRgb(theme.Accent);
            var logo = Colors.FromRgb(theme.Logo);
            string name = "item:theme:" + theme.Id;
            var card = AddCardFrame(name, Colors.FromRgb(theme.Background), isActive ? accent : logo.WithAlpha(0.18f), isActive ? 3 : 1,
                Loc.T(theme.NameKey), logo, preview, 42, pos);
            AddStatusBadge(card, accent, logo, isActive, owned, -48, owned ? (int?)null : theme.Price);
            AddTarget(name, card, new Vector2(_cardW, CardH), () => HandleTheme(theme, card), animate: false, haptic: false);
        }

        void AddCosmeticCard(CosmeticStyle style, Transform preview, float previewY, Vector2 pos)
        {
            var kind = style.Kind;
            bool isActive = S.Cosmetics.ActiveId(kind) == style.Id;
            bool owned = S.Cosmetics.Owns(kind, style.Id);
            string prefix = kind == CosmeticKind.BubbleStyle ? "bubbleStyle" : "trail";
            string name = $"item:{prefix}:{style.Id}";
            var card = AddCardFrame(name, Look.Background, isActive ? Look.Accent : Look.Logo.WithAlpha(0.18f), isActive ? 3 : 1,
                Loc.T(style.NameKey), Look.Logo, preview, previewY, pos);
            AddStatusBadge(card, Look.Accent, Look.Logo, isActive, owned, -48, owned ? (int?)null : style.Price);
            AddTarget(name, card, new Vector2(_cardW, CardH), () => HandleCosmetic(style, card), animate: false, haptic: false);
        }

        Transform TrailPreview(CosmeticStyle style)
        {
            var node = RenderShared.Node("Preview", null);
            var pts = new List<Vector2>();
            foreach (var (x, y) in new[] { (-58f, -8f), (-29f, 12f), (0f, -12f), (29f, 12f), (58f, -8f) }) pts.Add(Units.P(x, y));
            Rendering.TrailRenderer.Make(node, pts, Look.ParseTrail(style.Id), Look.Accent, 0, Z + 2);
            return node;
        }

        /// Cadre commun (ombre, fond, aperçu, titre).
        Transform AddCardFrame(string name, Color background, Color border, float borderWidth, string title, Color titleColor,
            Transform preview, float previewY, Vector2 pos)
        {
            var card = RenderShared.Node(name, Scroll.Content, Units.P(pos.x, pos.y));
            var size = new Vector2(_cardW, CardH);
            ShapeView.Rect("Shadow", card, Units.P(0, -5), size, 26, Colors.White(0, 0.08f), default, 0, Z - 1);
            var bg = ShapeView.Rect("Bg", card, Vector2.zero, size, 26, background, border, borderWidth, Z);
            Relief.Raise(bg, 5);
            preview.SetParent(card, false);
            preview.localPosition = Units.P(0, previewY);
            TenGoText.Create(card, title, Fonts.AvenirDemiBold, 22, titleColor, Units.P(0, -6), Z + 2).ShrinkToFit(_cardW - 20);
            return card;
        }

        void AddStatusBadge(Transform card, Color accent, Color logo, bool isActive, bool owned, float y, int? price)
        {
            var pos = Units.P(0, y);
            if (isActive)
            {
                var pill = ShapeView.Rect("Badge", card, pos, new Vector2(96, 32), 16, accent, default, 0, Z + 1);
                Relief.Raise(pill, 3);
                TenGoText.Create(card, Loc.T("shop.active"), Fonts.AvenirBold, 15, accent.ReadableInk(), pos, Z + 3).ShrinkToFit(88);
            }
            else if (owned)
            {
                ShapeView.Rect("Badge", card, pos, new Vector2(108, 32), 16, Color.clear, logo.WithAlpha(0.5f), 1.5f, Z + 1);
                TenGoText.Create(card, Loc.T("shop.select"), Fonts.AvenirMedium, 15, logo, pos, Z + 3).ShrinkToFit(100);
            }
            else
            {
                var label = TenGoText.Create(card, (price ?? 0).ToString(), Fonts.AvenirBold, 17, logo, Vector2.zero, Z + 3,
                    horizontal: HorizontalAlignmentOptions.Left);
                const float gap = 6;
                float contentW = 18 + gap + label.WidthPt();
                ShapeView.Rect("Badge", card, pos, new Vector2(contentW + 28, 32), 16, logo.WithAlpha(0.06f), logo.WithAlpha(0.18f), 1, Z + 1);
                float startX = -contentW / 2;
                CoinIcon.Make(card, Units.P(startX + 9, y), 9, Z + 3);
                label.transform.localPosition = Units.P(startX + 18 + gap, y);
            }
        }

        /// Solde, ancré par son bord droit ; renvoie sa largeur.
        float AddBalanceChip(float y)
        {
            var container = RenderShared.Node("balanceChip", Root);
            var label = TenGoText.Create(container, S.Economy.Balance.ToString(), Fonts.AvenirDemiBold, 24, GoldInk, Vector2.zero, Z + 12,
                horizontal: HorizontalAlignmentOptions.Left);
            const float gap = 9;
            float contentW = 24 + gap + label.WidthPt();
            float w = contentW + 44;
            container.localPosition = Units.P(UsableW / 2 - 24 - w / 2, y);
            _balanceChipCenter = Units.ToPoints(1) * (Vector2)container.localPosition;
            var pill = ShapeView.Rect("Pill", container, Vector2.zero, new Vector2(w, 46), 23, new Color(0.98f, 0.92f, 0.74f, 0.95f),
                new Color(0.84f, 0.64f, 0.28f, 0.4f), 1, Z + 10);
            Relief.Raise(pill, 4);
            float startX = -contentW / 2;
            CoinIcon.Make(container, Units.P(startX + 12, 0), 12, Z + 11);
            label.transform.localPosition = Units.P(startX + 24 + gap, 0);
            return w;
        }

        void AddBackButton(float y)
        {
            var node = RenderShared.Node("shopBack", Root, Units.P(0, y));
            var bg = ShapeView.Rect("Bg", node, Vector2.zero, new Vector2(210, 56), 28, Colors.White(0.96f, 0.95f), Colors.White(0.68f, 0.35f), 1, Z + 10);
            Relief.Raise(bg, 5);
            TenGoText.Create(node, Loc.T("shop.back"), Fonts.AvenirMedium, 19, Colors.White(0.32f), Vector2.zero, Z + 12).ShrinkToFit(190);
            AddTarget("shopBack", node, new Vector2(210, 56), GoBack, animate: false);
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
            var popup = TenGoText.Create(Root, "+" + amount, Fonts.AvenirHeavy, 26, new Color(0.85f, 0.60f, 0.15f),
                Units.P(_balanceChipCenter.x, _balanceChipCenter.y - 40), 300);
            var go = popup.gameObject;
            FxAlpha.Of(go);
            Tw.Run(go, Tw.Seq(Tw.Par(Tw.MoveBy(popup.transform, Units.P(0, 52), 0.8f),
                Tw.Seq(Tw.Wait(0.4f), Tw.FadeOut(go, 0.4f))), Tw.Remove(go)));
        }

        /// Message temporaire au-dessus du pied d'écran, sur fond opaque.
        void ShowToast(string text)
        {
            LastToast = text;
            if (_toast != null) RenderShared.Destroy(_toast.gameObject);
            var color = new Color(0.35f, 0.55f, 0.40f);
            var node = RenderShared.Node("Toast", Root, Units.P(0, _viewportBottom + 46));
            var go = node.gameObject;
            FxAlpha.Of(go).Alpha = 0;
            var label = TenGoText.Create(node, text, Fonts.AvenirDemiBold, 19, color, Vector2.zero, 402).Wrap(UsableW - 96);
            ShapeView.Rect("Bg", node, Vector2.zero, new Vector2(Mathf.Min(UsableW - 96, label.WidthPt()) + 44, label.HeightPt() + 26), 18,
                Colors.White(1, 0.96f), color.WithAlpha(0.35f), 1, 400);
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
