using System.Collections.Generic;
using System.Linq;
using TenGo.App;
using TenGo.Core;
using TenGo.Haptics;
using TenGo.Rendering;
using TenGo.Ui;
using TMPro;
using UnityEngine;

namespace TenGo.Screens
{
    /// Onglet Profil (ProfileScene.swift), refait pour la 4.0
    /// (`design/refonte/05_profil`) : niveau en tête (anneau d'XP), les six
    /// statistiques dans une seule carte 3 × 2, puis les accès en lignes :
    /// Ton cerveau, Missions du jour (pastille s'il y a une récompense à
    /// réclamer), Succès en médaillons, Classement. Le contenu défile sur un
    /// écran court. Les missions ne sont plus listées ici ; `Claim` reste pour
    /// les appelants.
    ///
    /// Cibles : « settings », « mirror », « missions », « achievements », « classement ».
    public sealed class ProfileScreen : ScreenBase
    {
        const int Z = 20;
        Transform _ui;
        float _cw;

        static float Pt(float v) => Design.Pt(v);

        public ProfileScreen(AppRoot app) : base(app, "ProfileScreen")
        {
            Rebuild();
        }

        /// Reconstruit tout l'écran (ouverture, puis après une réclamation).
        public void Rebuild()
        {
            if (_ui != null) RenderShared.Destroy(_ui.gameObject);
            if (TabBarNode != null) RenderShared.Destroy(TabBarNode.gameObject);
            Targets.Clear();
            _ui = RenderShared.Node("ProfileUI", Root);
            SetupUI();
        }

        void SetupUI()
        {
            _cw = ContentW;
            float right = _cw / 2;
            var gear = Design.IconButton("settings", _ui, new Vector2(right - Pt(22), HeaderRowY), VectorIconName.LineGear, Z + 10);
            AddTarget("settings", gear, Design.Pt(48, 48), OpenSettings, animate: false);
            AddTabTitle(Loc.T("profile.title"), _cw - Pt(60), _ui);

            AddTabBar(Tab.Progress);
            Scroll = new ScrollArea(_ui, HeaderBottom, TabBarTop, -UsableW / 2, UsableW / 2);
            var c = Scroll.Content;
            float cursor = -Pt(10);

            float h = Pt(96);
            AddLevelCard(c, cursor - h / 2, h);
            cursor -= h + Pt(12);

            h = Pt(160);
            AddStats(c, cursor - h / 2, h);
            cursor -= h + Pt(12);

            var mirror = S.Mirror;
            var look = mirror.WeeklyReading(MirrorSkill.Look).Value;
            string mirrorSub = mirror.GamesMeasured == 0
                ? Loc.T("mirror.card_hint")
                : Loc.F("mirror.card_summary", ProgressUi.DisplayValue(MirrorSkill.Look, look), mirror.RepertoireCount, Partition.Total);
            h = Pt(72);
            AddLink(c, "mirror", VectorIconName.LineBrain, Look.BubbleColor(8), Loc.T("mirror.title"), mirrorSub, cursor - h / 2, h, false,
                () => App.ShowMirror());
            cursor -= h + Pt(12);

            var missions = S.Missions.TodaysMissions;
            int done = missions.Count(m => m.IsCompleted);
            int toWin = missions.Where(m => !m.Claimed).Sum(m => m.Definition.CoinReward);
            AddLink(c, "missions", VectorIconName.LineCheck, Look.BubbleColor(5), Loc.T("missions.title"),
                Loc.F("ui.profile.missions_sub", done, missions.Count, toWin), cursor - h / 2, h, S.Missions.HasUnclaimedReward,
                () => App.ShowMissions());
            cursor -= h + Pt(20);

            var all = S.Achievements.Achievements();
            AddAchievements(c, all, ref cursor);
            cursor -= Pt(16);

            AddLink(c, "classement", VectorIconName.LineTrophy, Look.BubbleColor(3), Loc.T("menu.leaderboard"), Loc.T("ui.profile.leaderboard_sub"),
                cursor - h / 2, h, false, () => App.ShowLeaderboard());
            cursor -= h;

            Scroll.ContentHeight = -cursor + Pt(20);
            Scroll.ApplyClip();
        }

        void OpenSettings()
        {
            if (Overlay != null && Overlay.IsOpen) return;
            Overlay = App.Overlays.Settings(App, Root, () => App.ShowTutorial());
        }

        // MARK: - Blocs

        void AddLevelCard(Transform parent, float y, float height)
        {
            var card = RenderShared.Node("levelCard", parent, Units.P(0, y));
            Design.Surface("Bg", card, Vector2.zero, new Vector2(_cw, height), Pt(Design.CardRadius), Z);
            var level = S.Level.Breakdown;
            var ringPos = new Vector2(-_cw / 2 + Pt(16 + 32), 0);
            Design.LevelRing(card, ringPos, level, 29, 29.5f, 5, Z + 1);
            Design.Text(card, level.Level.ToString(), NunitoWeight.Black, 22, Design.Ink, ringPos, Z + 4).ShrinkToFit(Pt(44));

            float textX = ringPos.x + Pt(32 + 14);
            float textW = _cw / 2 - Pt(16) - textX;
            Design.Text(card, Loc.T(LevelCurve.TitleKey(level.Level)), NunitoWeight.Black, 20, Design.Ink, new Vector2(textX, Pt(18)), Z + 3,
                HorizontalAlignmentOptions.Left).ShrinkToFit(textW);
            int into = level.XpIntoLevel, forNext = level.XpForNext;
            string progress = forNext > 0 ? Loc.F("level.overlay.xp_progress", into, forNext) : Loc.T("level.title.ten_master");
            Design.Text(card, progress, NunitoWeight.Bold, 13, Design.Ink2, new Vector2(textX, -Pt(4)), Z + 3, HorizontalAlignmentOptions.Left)
                .ShrinkToFit(textW);
            float ratio = forNext > 0 ? Mathf.Clamp01(into / (float)forNext) : 1;
            Design.Bar(card, new Vector2(textX, -Pt(24)), textW / Design.Scale, 8, ratio, Look.BubbleColor(8), Z + 3);
        }

        void AddStats(Transform parent, float y, float height)
        {
            var card = RenderShared.Node("stats", parent, Units.P(0, y));
            Design.Surface("Bg", card, Vector2.zero, new Vector2(_cw, height), Pt(Design.CardRadius), Z);
            var stats = new (int value, string label)[]
            {
                (S.Games.BestScore, "profile.best_score"),
                (S.Games.RushBest, "ui.profile.rush_best"),
                (S.Streak.Current, "profile.daily_streak"),
                (S.Stats.BestChainEver, "profile.best_chain"),
                (S.Stats.PerfectBoardsTotal, "profile.perfect_count"),
                (S.Stats.TotalChainsMade, "ui.profile.chains"),
            };
            float cellW = _cw / 3, cellH = height / 2;
            ShapeView.Rect("H", card, Vector2.zero, new Vector2(_cw, Pt(1)), 0, Design.Line, default, 0, Z + 1);
            for (int i = 1; i < 3; i++)
                ShapeView.Rect("V", card, Units.P(-_cw / 2 + cellW * i, 0), new Vector2(Pt(1), height), 0, Design.Line, default, 0, Z + 1);
            for (int i = 0; i < stats.Length; i++)
            {
                float cx = -_cw / 2 + cellW * (i % 3 + 0.5f), cy = cellH / 2 - cellH * (i / 3);
                Design.Text(card, Design.Number(stats[i].value), NunitoWeight.Black, 22, Design.Ink, new Vector2(cx, cy + Pt(10)), Z + 2)
                    .ShrinkToFit(cellW - Pt(12));
                Design.Text(card, Loc.T(stats[i].label), NunitoWeight.ExtraBold, 11, Design.Ink2, new Vector2(cx, cy - Pt(14)), Z + 2)
                    .ShrinkToFit(cellW - Pt(12));
            }
        }

        /// Ligne d'accès : pastille d'icône teintée, titre, sous-titre, chevron.
        void AddLink(Transform parent, string name, VectorIconName icon, Color tint, string title, string subtitle, float y, float height,
            bool badged, System.Action action)
        {
            var row = RenderShared.Node(name, parent, Units.P(0, y));
            Design.Surface("Bg", row, Vector2.zero, new Vector2(_cw, height), Pt(Design.CardRadius), Z);
            float x = -_cw / 2 + Pt(14);
            Design.IconChip(row, new Vector2(x + Pt(20), 0), 40, 13, tint, icon, Z + 1, Design.Ink, 21);
            if (badged)
                ShapeView.Circle("Badge", row, Units.P(x + Pt(38), Pt(18)), Pt(6), Design.Danger, Design.Card, Pt(2), Z + 4);
            float textX = x + Pt(40 + 12);
            float textW = _cw / 2 - Pt(14 + 18 + 12) - textX;
            Design.Text(row, title, NunitoWeight.Black, 16, Design.Ink, new Vector2(textX, Pt(10)), Z + 2, HorizontalAlignmentOptions.Left)
                .ShrinkToFit(textW);
            Design.Text(row, subtitle, NunitoWeight.Bold, 12, Design.Ink2, new Vector2(textX, -Pt(11)), Z + 2, HorizontalAlignmentOptions.Left)
                .ShrinkToFit(textW);
            Design.Chevron(row, new Vector2(_cw / 2 - Pt(14 + 9), 0), Z + 2, 18, Design.Ink3);
            AddTarget(name, row, new Vector2(_cw, height), action, animate: false);
        }

        void AddAchievements(Transform parent, List<DisplayAchievement> all, ref float cursor)
        {
            var node = RenderShared.Node("achievements", parent, Units.P(0, cursor));
            float left = -_cw / 2;
            Design.Text(node, Loc.T("menu.achievements"), NunitoWeight.Black, 17, Design.Ink, new Vector2(left, -Pt(10)), Z + 2,
                HorizontalAlignmentOptions.Left);
            Design.Chevron(node, new Vector2(_cw / 2 - Pt(7), -Pt(10)), Z + 2, 14, Design.Ink3);
            Design.Text(node, all.Count(a => a.IsUnlocked) + " / " + all.Count, NunitoWeight.ExtraBold, 13, Design.Ink3,
                new Vector2(_cw / 2 - Pt(18), -Pt(10)), Z + 2, HorizontalAlignmentOptions.Right);

            float d = Pt(46), gap = Pt(8);
            int count = Mathf.Max(1, Mathf.Min(all.Count, (int)((_cw + gap) / (d + gap))));
            var shown = all.Where(a => a.IsUnlocked).Concat(all.Where(a => !a.IsUnlocked)).Take(count).ToList();
            float by = -Pt(20 + 12) - d / 2;
            int[] tints = { 3, 2, 6, 8, 4, 9, 7 };
            for (int i = 0; i < shown.Count; i++)
            {
                var a = shown[i];
                var pos = Units.P(left + d / 2 + i * (d + gap), by);
                var fill = a.IsUnlocked ? Design.Tint(Look.BubbleColor(tints[i % tints.Length]), 0.55f) : Design.Sunk;
                ShapeView.Circle("Badge", node, pos, d / 2, fill, default, 0, Z + 1);
                VectorIcon.Create(node, a.IsUnlocked ? CategoryIcon(a.Definition.Category) : VectorIconName.LineLock, pos, Pt(22),
                    a.IsUnlocked ? Design.Ink : Design.Ink3, Z + 2);
            }
            float blockH = Pt(20 + 12) + d;
            AddTarget("achievements", node, new Vector2(_cw, blockH), () => App.ShowAchievements(), animate: false, offsetPt: new Vector2(0, -blockH / 2));
            cursor -= blockH;
        }

        /// Icône au trait d'une catégorie de succès (partagée avec l'écran Succès).
        internal static VectorIconName CategoryIcon(AchievementCategory c) => c switch
        {
            AchievementCategory.Chain => VectorIconName.LineBolt,
            AchievementCategory.Perfect => VectorIconName.LineStar,
            AchievementCategory.Score => VectorIconName.LineTrophy,
            _ => VectorIconName.LineFlame,
        };

        /// Réclame une mission ; vrai si des pièces ont été créditées.
        public bool Claim(string id)
        {
            if (S.Missions.Claim(id) == null) return false;
            HapticManager.Medium();
            App.FlushSave();
            Rebuild();
            return true;
        }

        public override void Back() => App.ShowMenu();
    }
}
