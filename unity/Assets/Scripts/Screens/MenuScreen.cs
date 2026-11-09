using System.Collections.Generic;
using System.Linq;
using TenGo.App;
using TenGo.Core;
using TenGo.Fx;
using TenGo.Game;
using TenGo.Haptics;
using TenGo.Rendering;
using TenGo.Ui;
using TMPro;
using UnityEngine;

namespace TenGo.Screens
{
    /// Accueil, onglet Jouer (MenuScene.swift), refait pour la 4.0
    /// (`design/refonte/02_accueil`) : niveau avec anneau d'XP, pièces et
    /// réglages en haut, logo et slogan, une seule action dominante (« Jouer »,
    /// ou « Reprendre » au-dessus de « Nouvelle partie » quand une partie est
    /// sauvegardée, pastille de la graine), carte du Défi du jour (série,
    /// boucliers), cartes Rush / Duel / Puzzles, ligne « Vidéo : +10 », guide du
    /// premier achat et barre d'onglets.
    public sealed class MenuScreen : ScreenBase
    {
        const int Z = 20;
        const int RewardedCoins = EconomyRepository.RewardedMenuCoins;
        const int ShopGuideMaxSnooze = EconomyRepository.ShopGuideMaxSnooze;

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

        // MARK: - Mise en page (refonte 4.0, design/refonte/02_accueil)

        /// Largeur utile (visible moins 16 pt de chaque côté), en unités de scène.
        float _w;

        static float Pt(float v) => Design.Pt(v);

        void SetupUI()
        {
            _w = UsableW - 2 * Pt(Design.Gutter);
            float left = -_w / 2, right = _w / 2;
            float top = HalfH - TopInset;
            float rowY = top - Pt(32);

            // Rangée du haut : niveau (anneau d'XP, titre, XP), pièces, réglages.
            AddLevelBlock(left, rowY);
            var gear = Design.IconButton("parametres", Root, new Vector2(right - Pt(22), rowY), VectorIconName.LineGear, Z + 5);
            AddTarget("parametres", gear, Design.Pt(48, 48), OpenSettings);
            AddCoinChip(right - Pt(44 + 8), rowY);

            AddTabBar(Tab.Play);

            var saved = S.Games.Load();
            bool hasSave = saved != null;

            // Hauteurs de la maquette (pt) ; les marges s'ajustent à la hauteur disponible.
            const float rowH = 64, logoH = 54, tagGap = 6, tagH = 20, heroH = 68, secondGap = 10, secondH = 56,
                dailyH = 80, modesH = 120, rewardH = 52;
            float mLogo = 34, mHero = 30, mDaily = 14, mModes = 12, mReward = 12;
            float blocks = rowH + logoH + tagGap + tagH + heroH + (hasSave ? secondGap + secondH : 0) + dailyH + modesH + rewardH;
            float availablePt = (top - TabBarTop) / Design.Scale - 16;
            float margins = mLogo + mHero + mDaily + mModes + mReward;
            float spare = availablePt - blocks - margins;
            if (spare < 0)
            {
                // Écran court : les marges rétrécissent jusqu'à un plancher.
                float k = Mathf.Clamp01(1f + spare / (margins - 44f));
                mLogo = Mathf.Lerp(8, mLogo, k); mHero = Mathf.Lerp(14, mHero, k);
                mDaily = Mathf.Lerp(8, mDaily, k); mModes = Mathf.Lerp(8, mModes, k); mReward = Mathf.Lerp(6, mReward, k);
            }
            else if (spare > 130)
            {
                // Écran haut : un peu d'air autour du logo, le reste sous la pub (comme la maquette).
                float extra = Mathf.Min(spare - 130, 120);
                mLogo += extra * 0.35f;
                mHero += extra * 0.35f;
            }

            float y = top - Pt(rowH) - Pt(mLogo);
            AddLogo(y - Pt(logoH / 2));
            y -= Pt(logoH + tagGap);
            Design.Text(Root, Loc.T("menu.tagline"), NunitoWeight.Bold, 15, Design.Ink2, new Vector2(0, y - Pt(tagH / 2)), Z + 2)
                .ShrinkToFit(_w);
            y -= Pt(tagH + mHero);

            if (hasSave)
            {
                AddResumeButton(y - Pt(heroH / 2), Pt(heroH), saved);
                y -= Pt(heroH + secondGap);
                AddNewGameButton(y - Pt(secondH / 2), Pt(secondH), primary: false);
                y -= Pt(secondH);
            }
            else
            {
                AddNewGameButton(y - Pt(heroH / 2), Pt(heroH), primary: true);
                y -= Pt(heroH);
            }
            y -= Pt(mDaily);
            AddDailyCard(y - Pt(dailyH / 2), Pt(dailyH));
            y -= Pt(dailyH + mModes);
            AddModeTiles(y, Pt(modesH));
            y -= Pt(modesH + mReward);
            AddWatchAdButton(y - Pt(rewardH / 2), Pt(rewardH));
        }

        static TenGoText Label(Transform parent, string text, NunitoWeight weight, float size, Color color, Vector2 pos,
            int order, HorizontalAlignmentOptions align = HorizontalAlignmentOptions.Center) =>
            TenGoText.Create(parent, text, weight, size, color, Units.P(pos.x, pos.y), order, horizontal: align);

        /// Triangle « lecture » plein aux coins arrondis (icône `play` remplie de la maquette).
        static void FilledPlay(Transform parent, Vector2 center, float sizePt, Color color, int order)
        {
            float s = sizePt / 24f;
            Vector2 Q(float x, float yy) => Units.P(center.x + (x - 12) * s, center.y - (yy - 12) * s);
            var pts = new[] { Q(8, 5.5f), Q(8, 18.5f), Q(18, 12) };
            UiArt.FilledTriangle(parent, pts, color, order);
            StrokeView.Create("Edge", parent, new[] { pts[0], pts[1], pts[2], pts[0] }, color, 2.1f * s, order);
        }

        /// « Jouer » (action principale) ou « Nouvelle partie » (secondaire, sous « Reprendre »).
        void AddNewGameButton(float y, float height, bool primary)
        {
            var node = RenderShared.Node("newGame", Root, Units.P(0, y));
            var size = new Vector2(_w, height);
            Design.Surface("Bg", node, Vector2.zero, size, Pt(primary ? 22 : Design.ButtonRadius), Z,
                primary ? Design.Accent : Design.Card, hair: !primary);
            var ink = primary ? Design.OnAccent : Design.Ink;
            var label = Label(node, Loc.T(primary ? "menu.play" : "menu.new_game"), primary ? NunitoWeight.Black : NunitoWeight.ExtraBold,
                Pt(primary ? 22 : 17), ink, Vector2.zero, Z + 3, HorizontalAlignmentOptions.Left);
            float iconW = Pt(primary ? 24 : 22), gap = Pt(10);
            // La pastille de graine (« ×3 » ou « 1h 13min ») occupe le bord droit :
            // le contenu se centre dans la place restante et le libellé rétrécit
            // plutôt que de passer sous la pastille.
            float badgeW = AddSeedBadge(node, _w / 2 - Pt(14), primary);
            float left = -_w / 2 + Pt(16);
            float right = badgeW > 0 ? _w / 2 - Pt(14) - badgeW - Pt(10) : _w / 2 - Pt(16);
            float labelW = Mathf.Min(label.WidthPt(), right - left - iconW - gap);
            label.ShrinkToFit(labelW);
            labelW = Mathf.Min(labelW, label.WidthPt());
            float contentW = iconW + gap + labelW;
            float cx = badgeW > 0 ? Mathf.Min(0, (left + right) / 2) : 0;
            var iconPos = new Vector2(cx - contentW / 2 + iconW / 2, 0);
            if (primary) FilledPlay(node, iconPos, iconW, ink, Z + 2);
            else VectorIcon.Create(node, VectorIconName.LinePlay, Units.P(iconPos.x, iconPos.y), iconW, ink, Z + 2);
            label.transform.localPosition = Units.P(cx - contentW / 2 + iconW + gap, 0);
            AddTarget("newGame", node, size, OnNewGame);
        }

        /// Graine de dix : pastille au bord droit du bouton (pousse et temps restant, ou « ×N » fleurie).
        /// Rend la largeur de la pastille (0 sans graine).
        float AddSeedBadge(Transform button, float rightX, bool onAccent)
        {
            if (!(S.Seeds.Current is Seed seed)) return 0;
            bool bloomed = S.Seeds.IsBloomed;
            string text = bloomed ? "×" + seed.Power : RemainingText(seed.BloomAtMs);
            var badge = RenderShared.Node("seedBadge", button);
            var label = Label(badge, text, NunitoWeight.Black, Pt(13), Design.Ink, Vector2.zero, Z + 12, HorizontalAlignmentOptions.Left);
            float iconW = Pt(18), h = Pt(30);
            float w = Pt(10) + iconW + Pt(4) + label.WidthPt() + Pt(10);
            ShapeView.Rect("Bg", badge, Vector2.zero, new Vector2(w, h), h / 2,
                onAccent ? Colors.White(1, 0.55f).Flattened(Design.Accent) : Design.Sunk, default, 0, Z + 10);
            VectorIcon.Create(badge, VectorIconName.LineSeed, Units.P(-w / 2 + Pt(10) + iconW / 2, 0), iconW, Design.SeedGreen, Z + 11);
            label.transform.localPosition = Units.P(-w / 2 + Pt(10) + iconW + Pt(4), 0);
            badge.localPosition = Units.P(rightX - w / 2, 0);
            if (bloomed) SeedArt.Pulse(badge, 1.06f);
            return w;
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

        /// « Reprendre » : action principale quand une partie est en cours, score à droite.
        void AddResumeButton(float y, float height, SavedGame saved)
        {
            var node = RenderShared.Node("continuer", Root, Units.P(0, y));
            var size = new Vector2(_w, height);
            Design.Surface("Bg", node, Vector2.zero, size, Pt(22), Z, Design.Accent, hair: false);
            var ink = Design.OnAccent;
            var score = Label(node, Design.Number(saved.Score) + " " + Loc.T("game.points_label"), NunitoWeight.Bold, Pt(14),
                ink.WithAlpha(0.75f), new Vector2(_w / 2 - Pt(18), 0), Z + 3, HorizontalAlignmentOptions.Right);
            Label(node, Loc.T("ui.menu.resume"), NunitoWeight.Black, Pt(22), ink, new Vector2(-_w / 2 + Pt(18), 0), Z + 3,
                HorizontalAlignmentOptions.Left).ShrinkToFit(_w - score.WidthPt() - Pt(52));
            AddTarget("continuer", node, size, () => App.StartGame(GameLaunch.Normal(S.Games.Load() ?? saved)));
        }

        bool DailyDoneToday => S.Economy.IsDailyCompleted(DayKeys.Challenge(S.Clock));

        /// Carte blanche du Défi du jour : pastille calendrier teintée, titre, prochaine récompense, série.
        void AddDailyCard(float y, float height)
        {
            bool done = DailyDoneToday;
            var card = RenderShared.Node("dailyChallenge", Root, Units.P(0, y));
            FxAlpha.Of(card.gameObject).Alpha = done ? 0.62f : 1f;
            var size = new Vector2(_w, height);
            Design.Surface("Bg", card, Vector2.zero, size, Pt(Design.CardRadius), Z);

            float x = -_w / 2 + Pt(16);
            var tint = Look.BubbleColor(8);
            ShapeView.Rect("Chip", card, Units.P(x + Pt(24), 0), Design.Pt(48, 48), Pt(16), Design.Tint(tint), default, 0, Z + 1);
            VectorIcon.Create(card, VectorIconName.LineCalendar, Units.P(x + Pt(24), 0), Pt(24), Design.Ink, Z + 2);
            float textX = x + Pt(48 + 14);

            // Série : pastille enfoncée à droite (flamme, jours, boucliers).
            int streak = S.Streak.Current;
            int shields = S.Streak.ShieldCount;
            var days = Label(card, streak.ToString(), NunitoWeight.Black, Pt(14), Design.Ink, Vector2.zero, Z + 3, HorizontalAlignmentOptions.Left);
            float contentW = Pt(16 + 4) + days.WidthPt();
            TenGoText shieldCount = null;
            if (shields > 0)
            {
                shieldCount = Label(card, shields.ToString(), NunitoWeight.Black, Pt(14), Design.Ink2, Vector2.zero, Z + 3, HorizontalAlignmentOptions.Left);
                contentW += Pt(8 + 16 + 3) + shieldCount.WidthPt();
            }
            float chipW = contentW + Pt(20), chipH = Pt(30);
            float chipX = _w / 2 - Pt(16) - chipW / 2;
            ShapeView.Rect("StreakChip", card, Units.P(chipX, 0), new Vector2(chipW, chipH), chipH / 2, Design.Sunk, default, 0, Z + 1);
            float cx = chipX - contentW / 2;
            VectorIcon.Create(card, VectorIconName.LineFlame, Units.P(cx + Pt(8), 0), Pt(16), Design.Flame, Z + 3);
            cx += Pt(16 + 4);
            days.transform.localPosition = Units.P(cx, 0);
            cx += days.WidthPt();
            if (shieldCount != null)
            {
                cx += Pt(8);
                VectorIcon.Create(card, VectorIconName.IconShield, Units.P(cx + Pt(8), 0), Pt(16), Design.Ink2, Z + 3);
                shieldCount.transform.localPosition = Units.P(cx + Pt(16 + 3), 0);
            }

            float textW = chipX - chipW / 2 - Pt(10) - textX;
            Label(card, Loc.T("menu.daily"), NunitoWeight.Black, Pt(17), Design.Ink, new Vector2(textX, Pt(10)), Z + 3,
                HorizontalAlignmentOptions.Left).ShrinkToFit(textW);
            var next = StreakRepository.NextMilestone(streak);
            string sub = done ? Loc.T("menu.daily_done") : next != null ? Loc.F("streak.next_reward", next.Day) : "";
            if (sub.Length > 0)
                Label(card, sub, NunitoWeight.Bold, Pt(13), Design.Ink2, new Vector2(textX, -Pt(12)), Z + 3,
                    HorizontalAlignmentOptions.Left).ShrinkToFit(textW);

            // Déjà terminé aujourd'hui : grisé, non jouable jusqu'au lendemain.
            var t = AddTarget("dailyChallenge", card, size, () =>
            {
                if (DailyDoneToday) return;
                App.StartGame(GameLaunch.ForDaily());
            });
            t.Animate = !done;
        }

        /// Rush, Duel et Puzzles : trois cartes blanches, la couleur ne sert qu'à la pastille d'icône.
        void AddModeTiles(float topY, float height)
        {
            var levels = S.Catalog.Levels(1);
            int solved = levels.Count(l => S.Puzzles.Stars(l.World, l.Index) > 0);
            var tiles = new (string name, VectorIconName icon, string title, string subtitle, Color tint, System.Action action)[]
            {
                ("rush", VectorIconName.LineBolt, Loc.T("menu.rush"), Loc.T("ui.menu.rush_sub"), Look.BubbleColor(3),
                    () => App.StartGame(GameLaunch.Rush())),
                ("duel", VectorIconName.LineDuel, Loc.T("menu.duel"), Loc.T("menu.duel_sub"), Look.BubbleColor(7),
                    () => App.ShowDuel()),
                ("puzzles", VectorIconName.LinePuzzle, Loc.T("menu.puzzles"), solved + " / " + levels.Count, Look.BubbleColor(4),
                    () => App.ShowPuzzleLevels(1)),
            };
            float inner = Pt(10);
            float tileW = (_w - inner * (tiles.Length - 1)) / tiles.Length;
            float y = topY - height / 2;
            for (int i = 0; i < tiles.Length; i++)
            {
                var tile = tiles[i];
                var node = RenderShared.Node(tile.name, Root, Units.P(-_w / 2 + tileW / 2 + i * (tileW + inner), y));
                var size = new Vector2(tileW, height);
                Design.Surface("Bg", node, Vector2.zero, size, Pt(Design.CardRadius), Z);
                float h2 = height / 2;
                float icY = h2 - Pt(14 + 24);
                ShapeView.Rect("Chip", node, Units.P(0, icY), Design.Pt(48, 48), Pt(16), Design.Tint(tile.tint), default, 0, Z + 1);
                VectorIcon.Create(node, tile.icon, Units.P(0, icY), Pt(24), Design.Ink, Z + 2);
                Label(node, tile.title, NunitoWeight.Black, Pt(15), Design.Ink, new Vector2(0, h2 - Pt(80)), Z + 3).ShrinkToFit(tileW - Pt(16));
                Label(node, tile.subtitle, NunitoWeight.Bold, Pt(12), Design.Ink2, new Vector2(0, h2 - Pt(100)), Z + 3).ShrinkToFit(tileW - Pt(16));
                AddTarget(tile.name, node, size, tile.action);
            }
        }

        /// Pièces : capsule blanche, ancrée par son bord droit.
        void AddCoinChip(float rightEdgeX, float y)
        {
            var container = RenderShared.Node("coinChip", Root);
            float height = Pt(44);
            var number = Label(container, S.Economy.Balance.ToString(), NunitoWeight.Black, Pt(17), Design.Ink, Vector2.zero, Z + 3,
                HorizontalAlignmentOptions.Left);
            float width = Pt(10 + 20 + 8) + number.WidthPt() + Pt(14);
            Design.Surface("Bg", container, Vector2.zero, new Vector2(width, height), height / 2, Z);
            Design.Coin(container, new Vector2(-width / 2 + Pt(10 + 10), 0), 20, Z + 2);
            number.transform.localPosition = Units.P(-width / 2 + Pt(10 + 20 + 8), 0);
            container.localPosition = Units.P(rightEdgeX - width / 2, y);
            _coinLabel = number;
            _coinChip = container;
        }

        /// Niveau : rond blanc cerclé de l'anneau d'XP, titre du niveau et XP ; ouvre la Progression.
        void AddLevelBlock(float leftEdgeX, float y)
        {
            var container = RenderShared.Node("levelChip", Root, Units.P(leftEdgeX, y));
            var level = S.Level.Breakdown;
            float r = Pt(22);
            var discPos = new Vector2(r, 0);
            Design.LevelRing(container, discPos, level, 22, 23, 3, Z);
            int into = level.XpIntoLevel, forNext = level.XpForNext;
            Label(container, level.Level.ToString(), NunitoWeight.Black, Pt(16), Design.Ink, discPos, Z + 3).ShrinkToFit(r * 1.5f);

            float textX = Pt(44 + 10);
            var title = Label(container, Loc.T(LevelCurve.TitleKey(level.Level)), NunitoWeight.ExtraBold, Pt(13), Design.Ink,
                new Vector2(textX, Pt(7.5f)), Z + 3, HorizontalAlignmentOptions.Left);
            string xp = forNext > 0 ? Loc.F("level.overlay.xp_progress", into, forNext) : Loc.T("level.title.ten_master");
            var sub = Label(container, xp, NunitoWeight.Bold, Pt(12), Design.Ink3, new Vector2(textX, -Pt(8)), Z + 3,
                HorizontalAlignmentOptions.Left);
            float textW = Mathf.Max(title.WidthPt(), sub.WidthPt());
            float maxW = _w / 2 - textX - Pt(12);
            if (textW > maxW) { title.ShrinkToFit(maxW); sub.ShrinkToFit(maxW); textW = maxW; }
            float totalW = textX + textW;
            AddTarget("levelChip", container, new Vector2(totalW, Pt(48)), () => App.ShowProfile(), offsetPt: new Vector2(totalW / 2, 0));
        }

        /// Ligne « Vidéo : +10 (pièce) · N restantes » : pub récompensée rangée en bas, discrète.
        void AddWatchAdButton(float y, float height)
        {
            var node = RenderShared.Node("watchAd", Root, Units.P(0, y));
            FxAlpha.Of(node.gameObject);
            var size = new Vector2(_w, height);
            Design.Surface("Bg", node, Vector2.zero, size, Pt(Design.CardRadius), Z);
            float x = -_w / 2 + Pt(14);
            VectorIcon.Create(node, VectorIconName.LineVideo, Units.P(x + Pt(11), 0), Pt(22), Design.Ink, Z + 2);
            x += Pt(22 + 12);
            var label = Label(node, Loc.F("ui.menu.video", RewardedCoins), NunitoWeight.ExtraBold, Pt(14), Design.Ink,
                new Vector2(x, 0), Z + 2, HorizontalAlignmentOptions.Left);
            Design.Coin(node, new Vector2(x + label.WidthPt() + Pt(6 + 8), 0), 16, Z + 2);
            _watchAdBadge = Label(node, "", NunitoWeight.Bold, Pt(13), Design.Ink3, new Vector2(_w / 2 - Pt(14), 0), Z + 2,
                HorizontalAlignmentOptions.Right);
            _watchAd = node;
            RefreshWatchAdButton();
            AddTarget("watchAd", node, size, () => { AnimateTap(node); PresentRewardedAd(); }, animate: false);
        }

        void RefreshWatchAdButton()
        {
            int remaining = S.Rewarded.Policy.RemainingToday;
            _watchAdBadge.Text = Loc.F("ui.menu.video_left", remaining);
            FxAlpha.Of(_watchAd.gameObject).Alpha = remaining > 0 ? 1f : 0.45f;
        }

        /// Logo « TEN • GO » : 54 pt Black, point accent entre les deux mots.
        void AddLogo(float y)
        {
            var ten = Design.Text(Root, "TEN", NunitoWeight.Black, 54, Design.Ink, new Vector2(0, y), Z + 2, HorizontalAlignmentOptions.Left);
            var go = Design.Text(Root, "GO", NunitoWeight.Black, 54, Design.Ink, new Vector2(0, y), Z + 2, HorizontalAlignmentOptions.Left);
            ten.Tmp.characterSpacing = -2f;
            go.Tmp.characterSpacing = -2f;
            float wTen = ten.WidthPt(), wGo = go.WidthPt(), dot = Pt(18), gap = Pt(4);
            float total = wTen + gap + dot + gap + wGo;
            float x0 = -total / 2;
            ten.transform.localPosition = Units.P(x0, y);
            go.transform.localPosition = Units.P(x0 + wTen + gap * 2 + dot, y);
            var dotNode = RenderShared.Node("LogoDot", Root, Units.P(x0 + wTen + gap + dot / 2, y - Pt(2)));
            ShapeView.Circle("Dot", dotNode, Vector2.zero, dot / 2, Design.Accent, default, 0, Z + 3);
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
            var popup = TenGoText.Create(Root, "+" + RewardedCoins, NunitoWeight.Black, Pt(20), Design.GoldRim,
                (Vector2)_coinChip.localPosition + Units.P(0, -Pt(30)), 300);
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
