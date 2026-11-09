using System.Collections.Generic;
using System.Linq;
using TenGo.App;
using TenGo.Core;
using TenGo.Fx;
using TenGo.Game;
using TenGo.Haptics;
using TenGo.Rendering;
using TMPro;
using UnityEngine;

namespace TenGo.Screens
{
    /// Accueil, onglet Jouer (MenuScene.swift) : niveau, pièces et réglages en
    /// haut, logo et slogan, bouton « Jouer » (badge de la graine), « Reprendre »
    /// si une partie est sauvegardée, carte du Défi du jour (série, boucliers),
    /// tuiles Rush / Duel / Puzzles, bouton « pub contre +10 pièces », guide du
    /// premier achat et barre d'onglets.
    public sealed class MenuScreen : ScreenBase
    {
        const int Z = 20;
        const int RewardedCoins = EconomyRepository.RewardedMenuCoins;
        const int ShopGuideMaxSnooze = EconomyRepository.ShopGuideMaxSnooze;

        static readonly Color GoldFill = new(0.98f, 0.92f, 0.74f, 0.95f);
        static readonly Color GoldStroke = new(0.84f, 0.64f, 0.28f, 0.35f);
        static readonly Color GoldInk = new(0.45f, 0.34f, 0.10f, 1f);

        float _cardWidth = 340;
        TenGoText _coinLabel;
        Transform _coinChip;
        Transform _watchAd;
        TenGoText _watchAdBadge;
        CoachMarkOverlay _shopGuide;

        /// Guide du premier achat affiché (tests).
        public bool ShopGuideVisible => _shopGuide != null;

        public MenuScreen(AppRoot app) : base(app, "MenuScreen")
        {
            SetupUI();
            // Le bouton « regarder une pub » vient d'apparaître : préchargement ici.
            S.Rewarded.PreloadIfNeeded();
            MaybeShowShopPurchaseGuide();
        }

        // MARK: - Mise en page

        void SetupUI()
        {
            var theme = Look.Theme;
            float usableW = UsableW;
            float topRowY = TopRowY;
            float edgeX = usableW / 2 - 58;
            _cardWidth = Mathf.Min(usableW - 48, 600);

            // Barre du haut : niveau à gauche, pièces et réglages à droite.
            AddSettingsButton(new Vector2(edgeX, topRowY));
            AddCoinChip(edgeX - 36, topRowY);
            AddLevelChip(-_cardWidth / 2, topRowY);

            AddLogo(topRowY - 145);
            TenGoText.Create(Root, Loc.T("menu.tagline"), Fonts.AvenirMedium, 22, Look.Logo.WithAlpha(0.72f),
                Units.P(0, topRowY - 203), Z + 2).ShrinkToFit(usableW - 40);

            AddTabBar(Tab.Play);

            // Blocs d'action entre le slogan et les onglets : hauteurs fixes,
            // espacement calculé (« Reprendre » peut manquer sans laisser de trou).
            var saved = S.Games.Load();
            bool hasSave = saved != null;
            const float heroH = 140, resumeH = 80, dailyH = 206, tilesH = 190;
            var blocks = new List<float> { heroH };
            if (hasSave) blocks.Add(resumeH);
            blocks.Add(dailyH);
            blocks.Add(tilesH);

            float bandTop = topRowY - 245;
            float bandBottom = -HalfH + BottomInset + TabBar.Height + 18;
            float available = bandTop - bandBottom;
            float blocksH = blocks.Sum();
            float gap = Mathf.Min(80, Mathf.Max(24, (available - blocksH) / (blocks.Count - 1)));
            float cursor = bandTop - Mathf.Max(0, (available - (blocksH + gap * (blocks.Count - 1))) / 2);
            float Place(float h) { float c = cursor - h / 2; cursor -= h + gap; return c; }

            float heroY = Place(heroH);
            AddHeroButton(heroY, heroH, Look.BubbleColor(4));
            AddSeedBadge(heroY, heroH);
            if (hasSave) AddResumeButton(Place(resumeH), resumeH, saved);
            AddDailyCard(Place(dailyH), dailyH);
            AddModeTiles(Place(tilesH), tilesH);

            // « Pub récompensée », sous le solde qu'il alimente.
            AddWatchAdButton(new Vector2(edgeX - 52, topRowY - 82));
        }

        static TenGoText Label(Transform parent, string text, NunitoWeight weight, float size, Color color, Vector2 pos,
            int order, HorizontalAlignmentOptions align = HorizontalAlignmentOptions.Center) =>
            TenGoText.Create(parent, text, weight, size, color, Units.P(pos.x, pos.y), order, horizontal: align);

        /// Action primaire : pilule haute, icône + libellé.
        void AddHeroButton(float y, float height, Color accent)
        {
            var node = RenderShared.Node("newGame", Root, Units.P(0, y));
            var size = new Vector2(_cardWidth, height);
            ShapeView.Rect("Shadow", node, Units.P(0, -5), size, height / 2, Colors.White(0, 0.09f), default, 0, Z - 3);
            var bg = ShapeView.Rect("Bg", node, Vector2.zero, size, height / 2, accent, default, 0, Z);
            Relief.Raise(bg, 8);

            var ink = accent.ReadableInk();
            var label = Label(node, Loc.T("menu.play"), Fonts.AvenirBold, 34, ink, Vector2.zero, Z + 3, HorizontalAlignmentOptions.Left);
            const float iconSize = 76, inner = 20;
            float labelW = Mathf.Min(label.WidthPt(), _cardWidth - iconSize - inner - 60);
            label.ShrinkToFit(labelW);
            float contentW = iconSize + inner + labelW;
            var chipPos = Units.P(-contentW / 2 + iconSize / 2, 0);
            Relief.IconChip(node, chipPos, iconSize / 2, accent, Z + 1);
            VectorIcon.Create(node, VectorIconName.IconPlay, chipPos, 40, ink, Z + 2);
            label.transform.localPosition = Units.P(-contentW / 2 + iconSize + inner, 0);

            AddTarget("newGame", node, size, OnNewGame);
        }

        /// Graine de dix : badge en haut à droite du bouton « Jouer ».
        void AddSeedBadge(float heroY, float heroH)
        {
            if (!(S.Seeds.Current is Seed seed)) return;
            bool bloomed = S.Seeds.IsBloomed;
            string text = bloomed ? "×" + seed.Power : RemainingText(seed.BloomAtMs);
            var badge = RenderShared.Node("seedBadge", Root);
            var ink = bloomed ? new Color(0.36f, 0.22f, 0f) : new Color(0.25f, 0.36f, 0.15f);
            var label = Label(badge, text, Fonts.AvenirHeavy, 18, ink, Vector2.zero, Z + 12, HorizontalAlignmentOptions.Left);
            const float iconW = 32, h = 42;
            float w = 8 + iconW + 4 + label.WidthPt() + 14;
            ShapeView.Rect("Bg", badge, Vector2.zero, new Vector2(w, h), h / 2,
                bloomed ? SeedArt.GoldColor : new Color(0.90f, 0.96f, 0.84f), default, 0, Z + 10);
            var iconPos = Units.P(-w / 2 + 8 + iconW / 2, 0);
            if (bloomed) SeedArt.Flower(badge, iconPos, iconW, Z + 11, new Color(0.85f, 0.45f, 0.10f));
            else SeedArt.Sprout(badge, iconPos, iconW, Z + 11);
            label.transform.localPosition = Units.P(-w / 2 + 8 + iconW + 4, 0);
            badge.localPosition = Units.P(_cardWidth / 2 - w / 2 + 6, heroY + heroH / 2 - 6);
            if (bloomed) SeedArt.Pulse(badge, 1.1f);
            AddTarget("newGame", badge, new Vector2(w, h), OnNewGame);
        }

        /// `SeedGarden.remainingText` : « 3h 20min », « 45min » sous l'heure.
        string RemainingText(long bloomAtMs)
        {
            double seconds = Mathf.Max(60, (bloomAtMs - S.Clock.Now.ToUnixTimeMilliseconds()) / 1000f);
            int totalMin = (int)System.Math.Floor(seconds / 60);
            if (seconds < 3600) return totalMin + "min";
            int hours = totalMin / 60, minutes = totalMin % 60;
            return minutes == 0 ? hours + "h" : hours + "h " + minutes + "min";
        }

        /// Reprise : discrète, pour ne pas concurrencer « Jouer ».
        void AddResumeButton(float y, float height, SavedGame saved)
        {
            var node = RenderShared.Node("continuer", Root, Units.P(0, y));
            var size = new Vector2(_cardWidth, height);
            var bg = ShapeView.Rect("Bg", node, Vector2.zero, size, height / 2, Look.Logo.WithAlpha(0.07f), default, 0, Z);
            Relief.Raise(bg, 5, surface: true);
            Label(node, Loc.T("menu.continue"), Fonts.AvenirMedium, 22, Look.Logo, Vector2.zero, Z + 3).ShrinkToFit(_cardWidth - 40);
            AddTarget("continuer", node, size, () => App.StartGame(GameLaunch.Normal(S.Games.Load() ?? saved)));
        }

        bool DailyDoneToday => S.Economy.IsDailyCompleted(DayKeys.Challenge(S.Clock));

        /// Carte du Défi du jour, avec la série (flamme, boucliers, prochaine récompense).
        void AddDailyCard(float y, float height)
        {
            bool done = DailyDoneToday;
            var accent = done ? new Color(0.90f, 0.90f, 0.89f) : Look.BubbleColor(6);
            var ink = accent.ReadableInk();
            float half = height / 2;
            float leftX = -_cardWidth / 2 + 30;

            var card = RenderShared.Node("dailyChallenge", Root, Units.P(0, y));
            FxAlpha.Of(card.gameObject).Alpha = done ? 0.62f : 1f;
            var size = new Vector2(_cardWidth, height);
            var bg = ShapeView.Rect("Bg", card, Vector2.zero, size, 30, accent, default, 0, Z);
            Relief.Raise(bg, 6);

            var title = Label(card, Loc.T("menu.daily"), Fonts.AvenirBold, 27, ink, new Vector2(leftX, half - 41), Z + 3,
                HorizontalAlignmentOptions.Left);

            // Série : pastille claire en haut à droite.
            int streak = S.Streak.Current;
            int shields = S.Streak.ShieldCount;
            var days = Label(card, streak.ToString(), Fonts.AvenirHeavy, 20, ink, Vector2.zero, Z + 3, HorizontalAlignmentOptions.Left);
            float chipContentW = 26 + 6 + days.WidthPt();
            TenGoText shieldCount = null;
            if (shields > 0)
            {
                shieldCount = Label(card, shields.ToString(), Fonts.AvenirBold, 17, ink.WithAlpha(0.85f), Vector2.zero, Z + 3,
                    HorizontalAlignmentOptions.Left);
                chipContentW += 14 + 22 + 5 + shieldCount.WidthPt();
            }
            float chipW = chipContentW + 30;
            float chipY = half - 41;
            float chipX0 = _cardWidth / 2 - 24 - chipW / 2;
            ShapeView.Rect("StreakChip", card, Units.P(chipX0, chipY), new Vector2(chipW, 42), 21,
                Colors.White(1, accent.PerceivedLuminance() > 0.5f ? 0.55f : 0.16f), default, 0, Z + 1);
            float x = chipX0 - chipContentW / 2;
            VectorIcon.Create(card, VectorIconName.IconFlame, Units.P(x + 13, chipY), 26, ink, Z + 3);
            x += 26 + 6;
            days.transform.localPosition = Units.P(x, chipY);
            x += days.WidthPt();
            if (shieldCount != null)
            {
                x += 14;
                VectorIcon.Create(card, VectorIconName.IconShield, Units.P(x + 11, chipY), 22, ink.WithAlpha(0.85f), Z + 3);
                shieldCount.transform.localPosition = Units.P(x + 22 + 5, chipY);
            }
            title.ShrinkToFit(Mathf.Max(80, chipX0 - chipW / 2 - leftX - 12));

            var next = StreakRepository.NextMilestone(streak);
            if (next != null)
                Label(card, Loc.F("streak.next_reward", next.Day), Fonts.AvenirMedium, 16, ink.WithAlpha(0.85f),
                    new Vector2(leftX, half - 88), Z + 3, HorizontalAlignmentOptions.Left).ShrinkToFit(_cardWidth - 60);

            // Bandeau d'état en bas de carte : un vrai bouton clair tant que le défi est à jouer.
            const float pillH = 52;
            var pill = ShapeView.Rect("Pill", card, Units.P(0, -half + 46), new Vector2(_cardWidth - 56, pillH), pillH / 2,
                ink.WithAlpha(done ? 0.10f : 0.16f), default, 0, Z + 1);
            if (!done) Relief.Raise(pill, 4, surface: true);
            Label(card, Loc.T(done ? "menu.daily_done" : "menu.play"), Fonts.AvenirDemiBold, 18, done ? ink : Look.Logo,
                new Vector2(0, -half + 46), Z + 3).ShrinkToFit(_cardWidth - 90);

            // Déjà terminé aujourd'hui : grisé, non jouable jusqu'au lendemain.
            var t = AddTarget("dailyChallenge", card, size, () =>
            {
                if (DailyDoneToday) return;
                App.StartGame(GameLaunch.ForDaily());
            });
            t.Animate = !done;
        }

        /// Rush, Duel et Puzzles sur une ligne.
        void AddModeTiles(float y, float height)
        {
            var levels = S.Catalog.Levels(1);
            int solved = levels.Count(l => S.Puzzles.Stars(l.World, l.Index) > 0);
            var tiles = new (string name, VectorIconName icon, string title, string subtitle, Color accent, System.Action action)[]
            {
                ("rush", VectorIconName.IconRush, Loc.T("menu.rush"), Loc.T("menu.rush_sub"), Look.BubbleColor(2),
                    () => App.StartGame(GameLaunch.Rush())),
                ("duel", VectorIconName.IconDuel, Loc.T("menu.duel"), Loc.T("menu.duel_sub"), Look.BubbleColor(1),
                    () => App.ShowDuel()),
                ("puzzles", VectorIconName.IconPuzzle, Loc.T("menu.puzzles"), solved + "/" + levels.Count, Look.BubbleColor(9),
                    () => App.ShowPuzzleLevels(1)),
            };
            const float inner = 14;
            float tileW = (_cardWidth - inner * (tiles.Length - 1)) / tiles.Length;
            for (int i = 0; i < tiles.Length; i++)
            {
                var tile = tiles[i];
                var node = RenderShared.Node(tile.name, Root, Units.P(-_cardWidth / 2 + tileW / 2 + i * (tileW + inner), y));
                var size = new Vector2(tileW, height);
                var bg = ShapeView.Rect("Bg", node, Vector2.zero, size, 26, tile.accent, default, 0, Z);
                Relief.Raise(bg, 6);
                var ink = tile.accent.ReadableInk();
                Relief.IconChip(node, Units.P(0, 30), 42, tile.accent, Z + 1);
                VectorIcon.Create(node, tile.icon, Units.P(0, 30), 52, ink, Z + 2);
                Label(node, tile.title, Fonts.AvenirDemiBold, 19, ink, new Vector2(0, -28), Z + 3).ShrinkToFit(tileW - 16);
                Label(node, tile.subtitle, Fonts.AvenirMedium, 13, ink.WithAlpha(0.75f), new Vector2(0, -58), Z + 3).ShrinkToFit(tileW - 16);
                AddTarget(tile.name, node, size, tile.action);
            }
        }

        /// Bouton rond des réglages (SF Symbol `gearshape.fill`).
        void AddSettingsButton(Vector2 pos)
        {
            var node = RenderShared.Node("parametres", Root, Units.P(pos.x, pos.y));
            var bg = ShapeView.Circle("Bg", node, Vector2.zero, 24, Look.Logo.WithAlpha(0.10f), default, 0, Z + 5);
            Relief.Raise(bg, 4, surface: true);
            Glyphs.Gear(node, Vector2.zero, 24, Look.Logo, Relief.Surface, Z + 7);
            AddTarget("parametres", node, new Vector2(48, 48), OpenSettings);
        }

        /// Solde de pièces, ancré par son bord droit.
        void AddCoinChip(float rightEdgeX, float y)
        {
            var container = RenderShared.Node("coinChip", Root);
            const float coinR = 9, gap = 7, height = 40;
            var number = Label(container, S.Economy.Balance.ToString(), Fonts.AvenirBold, 19, Look.Logo, Vector2.zero, Z + 3,
                HorizontalAlignmentOptions.Left);
            float contentW = coinR * 2 + gap + number.WidthPt();
            float width = Mathf.Max(contentW + 40, 70);
            var bg = ShapeView.Rect("Bg", container, Vector2.zero, new Vector2(width, height), height / 2, GoldFill, default, 0, Z);
            Relief.Raise(bg, 4, surface: true);
            float startX = -contentW / 2;
            CoinIcon.Make(container, Units.P(startX + coinR, 0), coinR, Z + 2);
            number.transform.localPosition = Units.P(startX + coinR * 2 + gap, 0);
            container.localPosition = Units.P(rightEdgeX - width / 2, y);
            _coinLabel = number;
            _coinChip = container;
        }

        /// Niveau du joueur, ancré par son bord gauche ; ouvre la Progression.
        void AddLevelChip(float leftEdgeX, float y)
        {
            var container = RenderShared.Node("levelChip", Root);
            var label = Label(container, Loc.F("level.chip.label", S.Level.Level), Fonts.AvenirBold, 18, Look.Logo,
                new Vector2(9, 0), Z + 3);
            float labelW = label.WidthPt();
            const float height = 40;
            float width = Mathf.Max(labelW + 58, 88);
            var bg = ShapeView.Rect("Bg", container, Vector2.zero, new Vector2(width, height), height / 2,
                Look.Accent.WithAlpha(0.14f), default, 0, Z);
            Relief.Raise(bg, 4, surface: true);
            ShapeView.Circle("Dot", container, Units.P(-labelW / 2 - 2, 0), 6, Look.BubbleColor(6), default, 0, Z + 2);
            container.localPosition = Units.P(leftEdgeX + width / 2, y);
            AddTarget("levelChip", container, new Vector2(width, height), () => App.ShowProfile());
        }

        /// Bouton rond « regarder une pub → +10 pièces » et badge du quota restant.
        void AddWatchAdButton(Vector2 pos)
        {
            var node = RenderShared.Node("watchAd", Root, Units.P(pos.x, pos.y));
            FxAlpha.Of(node.gameObject);
            const float r = 38;
            var bg = ShapeView.Circle("Bg", node, Vector2.zero, r, new Color(0.98f, 0.92f, 0.74f, 0.97f), default, 0, Z + 6);
            Relief.Raise(bg, 4);
            UiArt.FilledTriangle(node, new[] { Units.P(-6, 20), Units.P(-6, 2), Units.P(11, 11) }, Colors.White(0.34f), Z + 8);

            var label = Label(node, "+" + RewardedCoins, Fonts.AvenirBold, 15, GoldInk, Vector2.zero, Z + 8, HorizontalAlignmentOptions.Left);
            const float gap = 4;
            float contentW = 14 + gap + label.WidthPt();
            CoinIcon.Make(node, Units.P(-contentW / 2 + 7, -15), 7, Z + 8);
            label.transform.localPosition = Units.P(-contentW / 2 + 14 + gap, -15);

            ShapeView.Circle("BadgeBg", node, Units.P(27, 27), 11, Colors.White(1, 0.96f), new Color(0.84f, 0.64f, 0.28f, 0.55f), 1, Z + 9);
            _watchAdBadge = Label(node, "", Fonts.AvenirBold, 13, GoldInk, new Vector2(27, 27), Z + 10);
            _watchAd = node;
            RefreshWatchAdButton();
            AddTarget("watchAd", node, new Vector2(r * 2, r * 2), () => { AnimateTap(node); PresentRewardedAd(); }, animate: false);
        }

        void RefreshWatchAdButton()
        {
            int remaining = S.Rewarded.Policy.RemainingToday;
            _watchAdBadge.Text = remaining.ToString();
            FxAlpha.Of(_watchAd.gameObject).Alpha = remaining > 0 ? 1f : 0.45f;
        }

        /// Logo « TEN • GO » : bulle d'une couleur tirée au hasard, qui pulse.
        void AddLogo(float y)
        {
            TenGoText.Create(Root, "TEN", Fonts.AvenirHeavy, 72, Look.Logo, Units.P(-18, y), Z + 2, horizontal: HorizontalAlignmentOptions.Right);
            TenGoText.Create(Root, "GO", Fonts.AvenirHeavy, 72, Look.Logo, Units.P(18, y), Z + 2, horizontal: HorizontalAlignmentOptions.Left);
            var dotNode = RenderShared.Node("LogoDot", Root, Units.P(0, y + 4));
            ShapeView.Circle("Dot", dotNode, Vector2.zero, 14, Look.BubbleColor(Random.Range(1, 10)), default, 0, Z + 3);
            Tw.Run(dotNode.gameObject, Tw.Forever(Tw.Seq(Tw.Scale(dotNode, 1.15f, 1.4f), Tw.Scale(dotNode, 0.90f, 1.4f))));
        }

        // MARK: - Actions

        void OnNewGame()
        {
            if (!S.Settings.HasSeenTutorial) { App.ShowTutorial(); return; }
            S.Games.Clear();
            App.StartGame(GameLaunch.Normal());
        }

        /// Réglages (`presentSettings`) ; « Revoir le tutoriel » ramène au tutoriel.
        public void OpenSettings()
        {
            if (Overlay != null && Overlay.IsOpen) return;
            Overlay = App.Overlays.Settings(App, Root, () => App.ShowTutorial());
        }

        public bool SettingsOpen => Overlay != null && Overlay.IsOpen;

        void PresentRewardedAd()
        {
            if (!S.Rewarded.Policy.CanWatchToday)
            {
                // Quota du jour atteint : retour + bouton grisé.
                RefreshWatchAdButton();
                ShowUnavailableFeedback();
                return;
            }
            S.Rewarded.Show(RewardedPlacement.MenuCoins, () =>
            {
                if (Root == null) return;
                S.Economy.AddCoins(RewardedCoins);
                App.FlushSave();
                RefreshCoins();
                RefreshWatchAdButton();
                ShowRewardFeedback();
            }, _ => { }, () => { if (Root != null) ShowUnavailableFeedback(); });
        }

        void RefreshCoins()
        {
            _coinLabel.Text = S.Economy.Balance.ToString();
            Tw.Run(_coinChip.gameObject, Tw.Seq(Tw.Scale(_coinChip, 1.12f, 0.08f), Tw.Scale(_coinChip, 1f, 0.12f)), "bump");
        }

        void ShowRewardFeedback()
        {
            var popup = TenGoText.Create(Root, "+" + RewardedCoins, Fonts.AvenirHeavy, 26, new Color(0.85f, 0.60f, 0.15f),
                (Vector2)_coinChip.localPosition + Units.P(0, -30), 300);
            var go = popup.gameObject;
            FxAlpha.Of(go);
            Tw.Run(go, Tw.Seq(Tw.Par(Tw.MoveBy(popup.transform, Units.P(0, 52), 0.8f),
                Tw.Seq(Tw.Wait(0.4f), Tw.FadeOut(go, 0.4f))), Tw.Remove(go)));
        }

        void ShowUnavailableFeedback() => Shake(_watchAd, 6);

        // MARK: - Guide d'achat (premier booster)

        /// Premier moment où le solde couvre un indice : coach-mark sur l'onglet
        /// Boutique. Le drapeau n'est posé qu'à la réussite de l'achat ; au-delà
        /// de trois refus, le guide n'est plus proposé.
        void MaybeShowShopPurchaseGuide()
        {
            if (!S.Settings.HasSeenTutorial || S.Economy.ShopGuideCompleted || S.Economy.Balance < Booster.Hint.Price()) return;
            var target = TabBarNode != null ? TabBarNode.Find("tab_shop") : null;
            if (target == null) return;
            if (S.Economy.ShopGuideSnooze >= ShopGuideMaxSnooze)
            {
                S.Economy.MarkShopGuideCompleted();
                S.Analytics.ShopGuide("gaveup");
                return;
            }
            After(0.45f, () =>
            {
                if (_shopGuide != null || target == null) return;
                _shopGuide = CoachMarkOverlay.Present(Root, target, Loc.T("guide.shop_menu"), SceneSize);
                S.Analytics.ShopGuide("shown");
            });
        }

        protected override bool InterceptDown(Vector2 pt)
        {
            if (_shopGuide == null) return false;
            var guide = _shopGuide;
            _shopGuide = null;
            HapticManager.Light();
            if (guide.HandleTouch(pt) == CoachMarkOverlay.TouchResult.Target)
            {
                S.Analytics.ShopGuide("opened");
                var tab = TabBarNode.Find("tab_shop");
                AnimateTap(tab);
                After(0.12f, () => App.ShowShop(false, true));
            }
            else
            {
                S.Economy.SnoozeShopGuide();
                S.Analytics.ShopGuide("later");
            }
            return true;
        }

        /// L'accueil est la racine : le retour ne mène nulle part (sauf fermer les réglages).
        public override void Back()
        {
            if (Overlay != null && Overlay.IsOpen) Overlay.Close();
        }
    }
}
