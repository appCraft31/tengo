using System.Collections.Generic;
using System.Linq;
using TenGo.App;
using TenGo.Core;
using TenGo.Haptics;
using TenGo.Rendering;
using UnityEngine;

namespace TenGo.Screens
{
    /// Onglet Progression (ProfileScene.swift) : carte de niveau (XP), accès à
    /// « Ton cerveau », six tuiles de statistiques, les deux missions du jour à
    /// mettre en avant (réclamables ici), les succès en médaillons et l'accès
    /// au classement. Tout se tasse (`squeeze`) sur un écran trop court.
    ///
    /// Cibles : « mirror », « missions », « claim_&lt;id&gt; », « achievements »,
    /// « classement ».
    public sealed class ProfileScreen : ScreenBase
    {
        const int Z = 20;
        Transform _ui;
        float _cardWidth = 340;
        float _squeeze = 1;

        static Vector2 P(float x, float y) => Units.P(x, y);
        static Color Tint(int v) => Look.BubbleColor(v);

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
            var logo = Look.Logo;
            float usableWidth = UsableW;
            float halfH = HalfH;
            _cardWidth = Mathf.Min(usableWidth - 48, 600);

            float titleY = halfH - TopInset - 36;
            TenGoText.Create(_ui, Loc.T("profile.title"), Fonts.AvenirHeavy, 36, logo, P(0, titleY), Z).ShrinkToFit(usableWidth - 40);

            AddTabBar(Tab.Progress);

            const float headerH = 150, tileH = 92, tileGap = 12, sectionH = 34, missionH = 88, missionGap = 12;
            const float badgesH = 84, navH = 58, mirrorH = 96;
            var missions = FeaturedMissions();
            float missionsBlock = missions.Count == 0 ? 0
                : sectionH + missions.Count * missionH + (missions.Count - 1) * missionGap + 22;
            float nominal = headerH + 18 + mirrorH + 18 + tileH * 3 + tileGap * 2 + 22 + missionsBlock + sectionH + badgesH + 16 + navH;

            float top = titleY - 48;
            float available = top - (TabBarTop + 14);
            _squeeze = Mathf.Min(1, available / nominal);
            float cursor = top;
            float Place(float h) { float c = cursor - h * _squeeze / 2; cursor -= h * _squeeze; return c; }
            void Gap(float h) => cursor -= h * _squeeze;

            AddLevelCard(Place(headerH), headerH * _squeeze);
            Gap(18);
            AddMirrorCard(Place(mirrorH), mirrorH * _squeeze);
            Gap(18);

            var stats = new (string value, string label, Color tint)[]
            {
                (S.Games.BestScore.ToString(), Loc.T("profile.best_score"), Tint(3)),
                (S.Streak.Current.ToString(), Loc.T("profile.daily_streak"), Tint(2)),
                (S.Stats.BestChainEver.ToString(), Loc.T("profile.best_chain"), Tint(5)),
                (S.Stats.PerfectBoardsTotal.ToString(), Loc.T("profile.perfect_count"), Tint(7)),
                (S.Games.RushBest.ToString(), Loc.T("profile.rush_best"), Tint(4)),
                (S.Stats.TotalChainsMade.ToString(), Loc.T("profile.total_chains"), Tint(9)),
            };
            float tileW = (_cardWidth - tileGap) / 2;
            for (int row = 0; row < 3; row++)
            {
                float y = Place(tileH);
                for (int column = 0; column < 2; column++)
                {
                    var st = stats[row * 2 + column];
                    AddStatTile(st.value, st.label, st.tint, new Vector2((column == 0 ? -1 : 1) * (tileW + tileGap) / 2, y),
                        new Vector2(tileW, tileH * _squeeze));
                }
                if (row < 2) Gap(tileGap);
            }
            Gap(22);

            if (missions.Count > 0)
            {
                AddSectionHeader(Loc.T("menu.missions"), null, "missions", Place(sectionH), S.Missions.HasUnclaimedReward,
                    () => App.ShowMissions());
                for (int i = 0; i < missions.Count; i++)
                {
                    AddMissionRow(missions[i], Place(missionH), missionH * _squeeze);
                    if (i < missions.Count - 1) Gap(missionGap);
                }
                Gap(22);
            }

            var all = S.Achievements.Achievements();
            AddSectionHeader(Loc.T("menu.achievements"), all.Count(a => a.IsUnlocked) + " / " + all.Count, "achievements",
                Place(sectionH), false, () => App.ShowAchievements());
            AddAchievementBadges(all, Place(badgesH), badgesH * _squeeze);
            Gap(16);

            AddNavRow(Loc.T("menu.leaderboard"), "classement", Place(navH), (navH - 6) * _squeeze, () => App.ShowLeaderboard());

            // « Réclamer » d'abord : posé sur une rangée elle-même cliquable (ajouté en dernier = testé en premier).
            foreach (var t in _claims) Targets.Add(t);
            _claims.Clear();
        }

        readonly List<Target> _claims = new();

        /// Deux missions : à réclamer, puis en cours, enfin déjà réclamées.
        List<DisplayMission> FeaturedMissions()
        {
            int Rank(DisplayMission m) => m.Claimed ? 2 : m.IsCompleted ? 0 : 1;
            return S.Missions.TodaysMissions.Select((m, i) => (m, i)).OrderBy(p => Rank(p.m)).ThenBy(p => p.i)
                .Select(p => p.m).Take(2).ToList();
        }

        // MARK: - Blocs

        void AddLevelCard(float y, float height)
        {
            var tint = Tint(6);
            var ink = tint.ReadableInk();
            var card = RenderShared.Node("levelCard", _ui, P(0, y));
            var bg = ShapeView.Rect("Bg", card, Vector2.zero, new Vector2(_cardWidth, height), 30, tint, default, 0, Z);
            Relief.Raise(bg, 6);

            float bubbleR = Mathf.Min(46, height / 2 - 14);
            float bubbleX = -_cardWidth / 2 + 26 + bubbleR;
            var bubble = ShapeView.Circle("Bubble", card, P(bubbleX, 0), bubbleR, Color.white, default, 0, Z + 3);
            Relief.Raise(bubble, 4);
            var level = S.Level.Breakdown;
            TenGoText.Create(card, level.Level.ToString(), Fonts.AvenirHeavy, 38, Colors.White(0.24f), P(bubbleX, 0), Z + 5)
                .ShrinkToFit(bubbleR * 1.5f);

            float textX = bubbleX + bubbleR + 22;
            float textW = _cardWidth / 2 - 26 - textX;
            ProgressUi.Left(card, Loc.T(LevelCurve.TitleKey(level.Level)), Fonts.AvenirBold, 26, ink, textX, 30, Z + 3, textW);

            int into = level.XpIntoLevel, forNext = level.XpForNext;
            string progressText = forNext > 0 ? Loc.F("level.overlay.xp_progress", into, forNext) : Loc.T("level.title.ten_master");
            ProgressUi.Left(card, progressText, Fonts.AvenirDemiBold, 16, ink.WithAlpha(0.85f), textX, -2, Z + 3);

            float ratio = forNext > 0 ? Mathf.Clamp01(into / (float)forNext) : 1;
            ProgressUi.Bar(card, textW, ratio, Colors.White(1, 0.55f), tint.LedgeShade().LedgeShade(), textX, -32, Z + 3);
        }

        void AddStatTile(string value, string label, Color tint, Vector2 pos, Vector2 size)
        {
            var fill = tint.MixedWithWhite(0.45f);
            var ink = fill.ReadableInk();
            var tile = RenderShared.Node("Stat", _ui, P(pos.x, pos.y));
            ShapeView.Rect("Bg", tile, Vector2.zero, size, 24, fill, default, 0, Z);
            TenGoText.Create(tile, value, Fonts.AvenirHeavy, 30, ink, P(0, 13), Z + 2).ShrinkToFit(size.x - 20);
            TenGoText.Create(tile, label, Fonts.AvenirMedium, 15, ink.WithAlpha(0.8f), P(0, -20), Z + 2).ShrinkToFit(size.x - 20);
        }

        void AddSectionHeader(string text, string trailing, string name, float y, bool badged, System.Action action)
        {
            var header = RenderShared.Node(name, _ui, P(0, y));
            var label = ProgressUi.Left(header, text, Fonts.AvenirBold, 22, Look.Logo, -_cardWidth / 2 + 4, 0, Z + 2);
            var trailColor = Look.Logo.WithAlpha(0.65f);
            // « › » redessiné ; le compteur éventuel le précède (« 9 / 26  › »).
            ProgressUi.Chevron(header, new Vector2(_cardWidth / 2 - 10, 0), 17, trailColor, Z + 2);
            if (trailing != null)
                ProgressUi.Right(header, trailing, Fonts.AvenirDemiBold, 17, trailColor, _cardWidth / 2 - 24, 0, Z + 2);
            if (badged)
                ShapeView.Circle("Badge", header, P(-_cardWidth / 2 + 4 + label.WidthPt() + 14, 8), 7,
                    new Color(0.92f, 0.32f, 0.30f), Colors.White(1, 0.9f), 1.5f, Z + 3);
            AddTarget(name, header, new Vector2(_cardWidth, 44), action, animate: false);
        }

        void AddMissionRow(DisplayMission mission, float y, float height)
        {
            var def = mission.Definition;
            const float rightColumnW = 148;
            float textLeft = -_cardWidth / 2 + 22;
            float textW = _cardWidth - rightColumnW - 36;
            var logo = Look.Logo;

            var row = RenderShared.Node("missions", _ui, P(0, y));
            var bg = ShapeView.Rect("Bg", row, Vector2.zero, new Vector2(_cardWidth, height), 24, Relief.Surface, default, 0, Z);
            Relief.Raise(bg, 4);
            AddTarget("missions", row, new Vector2(_cardWidth, height), () => App.ShowMissions(), animate: false);

            ProgressUi.Left(row, ProgressUi.MissionTitle(def), Fonts.AvenirDemiBold, 17, logo, textLeft, 16, Z + 2, textW);
            float ratio = def.Target > 0 ? Mathf.Clamp01(mission.Progress / (float)def.Target) : 0;
            var count = ProgressUi.Right(row, Mathf.Min(mission.Progress, def.Target) + " / " + def.Target, Fonts.AvenirMedium, 14,
                logo.WithAlpha(0.7f), textLeft + textW, -16, Z + 2);
            // La barre s'arrête avant le compteur (« 6000 / 6000 » dépasse les 74 pt réservés par iOS).
            float barW = Mathf.Min(textW - 74, textW - count.WidthPt() - 10);
            ProgressUi.Bar(row, barW, ratio, logo.WithAlpha(0.12f),
                mission.IsCompleted ? Tint(4).LedgeShade() : Tint(7), textLeft, -16, Z + 2);

            float rightX = _cardWidth / 2 - rightColumnW / 2 - 4;
            if (mission.Claimed)
            {
                ProgressUi.Check(row, new Vector2(rightX, 0), 30, Tint(4).LedgeShade().LedgeShade(), Z + 2);
            }
            else if (mission.IsCompleted)
            {
                var claim = RenderShared.Node("claim_" + def.Id, row, P(rightX, 2));
                var accent = Tint(4);
                var cbg = ShapeView.Rect("Bg", claim, Vector2.zero, new Vector2(128, 48), 24, accent, default, 0, Z + 4);
                Relief.Raise(cbg, 4);
                TenGoText.Create(claim, Loc.T("missions.claim"), Fonts.AvenirBold, 16, accent.ReadableInk(), Vector2.zero, Z + 6)
                    .ShrinkToFit(110);
                var t = new Target { Name = "claim_" + def.Id, Node = claim, SizePt = new Vector2(128, 48), Animate = false, Haptic = false };
                string id = def.Id;
                t.Action = () => Claim(id);
                _claims.Add(t);
            }
            else
            {
                var reward = TenGoText.Create(row, "+" + def.CoinReward, Fonts.AvenirBold, 17, new Color(0.45f, 0.34f, 0.10f),
                    Vector2.zero, Z + 3, horizontal: TMPro.HorizontalAlignmentOptions.Left);
                float contentW = 20 + 7 + reward.WidthPt();
                ShapeView.Rect("Chip", row, P(rightX, 0), new Vector2(contentW + 30, 40), 20, new Color(0.99f, 0.95f, 0.80f), default, 0, Z + 2);
                CoinIcon.Make(row, P(rightX - contentW / 2 + 10, 0), 10, Z + 3);
                reward.transform.localPosition = P(rightX - contentW / 2 + 27, 0);
            }
        }

        /// Réclame une mission (bouton posé sur la rangée).
        public bool Claim(string id)
        {
            if (S.Missions.Claim(id) == null) return false;
            HapticManager.Medium();
            App.FlushSave();
            Rebuild();
            return true;
        }

        void AddAchievementBadges(List<DisplayAchievement> all, float y, float height)
        {
            var row = RenderShared.Node("achievementsBadges", _ui, P(0, y));
            AddTarget("achievements", row, new Vector2(_cardWidth, height), () => App.ShowAchievements(), animate: false);
            float radius = Mathf.Min(36, height / 2 - 6);
            float step = radius * 2 + 16;
            int count = Mathf.Max(1, Mathf.Min(all.Count, (int)((_cardWidth + 16) / step)));
            var shown = all.Where(a => a.IsUnlocked).Concat(all.Where(a => !a.IsUnlocked)).Take(count).ToList();
            float startX = -_cardWidth / 2 + radius;
            int[] tints = { 3, 2, 5, 7, 4, 9, 6 };
            for (int i = 0; i < shown.Count; i++)
            {
                var item = shown[i];
                var center = new Vector2(startX + i * step, 2);
                var tint = Tint(tints[i % 7]);
                var medal = ShapeView.Circle("Medal", row, P(center.x, center.y), radius,
                    item.IsUnlocked ? tint : Look.Logo.WithAlpha(0.10f), default, 0, Z);
                if (item.IsUnlocked) Relief.Raise(medal, 4);
                var ink = item.IsUnlocked ? tint.ReadableInk() : Look.Logo.WithAlpha(0.45f);
                ProgressUi.CategoryIcon(row, center, item.Definition.Category, radius * 0.9f, ink, Z + 2, locked: !item.IsUnlocked);
            }
        }

        void AddMirrorCard(float y, float height)
        {
            var node = RenderShared.Node("mirror", _ui, P(0, y));
            var fill = Look.Accent.MixedWithWhite(0.30f);
            var ink = fill.ReadableInk();
            var bg = ShapeView.Rect("Bg", node, Vector2.zero, new Vector2(_cardWidth, height), 28, fill, default, 0, Z);
            Relief.Raise(bg, 5);
            VectorIcon.Create(node, VectorIconName.IconProgress, P(-_cardWidth / 2 + 44, 0), 34, ink, Z + 2);

            var mirror = S.Mirror;
            ProgressUi.Left(node, Loc.T("mirror.title"), Fonts.AvenirBold, 22, ink, -_cardWidth / 2 + 84, height * 0.17f, Z + 2);
            var look = mirror.WeeklyReading(MirrorSkill.Look).Value;
            string summary = mirror.GamesMeasured == 0
                ? Loc.T("mirror.card_hint")
                : Loc.F("mirror.card_summary", ProgressUi.DisplayValue(MirrorSkill.Look, look), mirror.RepertoireCount, Partition.Total);
            ProgressUi.Left(node, summary, Fonts.AvenirMedium, 15, ink.WithAlpha(0.8f), -_cardWidth / 2 + 84, -height * 0.2f, Z + 2,
                _cardWidth - 84 - 50);
            ProgressUi.Chevron(node, new Vector2(_cardWidth / 2 - 30, 2), 30, ink.WithAlpha(0.7f), Z + 2);
            AddTarget("mirror", node, new Vector2(_cardWidth, height), () => App.ShowMirror(), animate: false);
        }

        void AddNavRow(string label, string name, float y, float height, System.Action action)
        {
            var row = RenderShared.Node(name, _ui, P(0, y));
            var bg = ShapeView.Rect("Bg", row, Vector2.zero, new Vector2(_cardWidth, height), height / 2, Relief.Surface, default, 0, Z);
            Relief.Raise(bg, 4);
            ProgressUi.Left(row, label, Fonts.AvenirDemiBold, 18, Look.Logo, -_cardWidth / 2 + 26, 0, Z + 2);
            ProgressUi.Chevron(row, new Vector2(_cardWidth / 2 - 30, 2), 26, Look.Logo.WithAlpha(0.6f), Z + 2);
            AddTarget(name, row, new Vector2(_cardWidth, height), action, animate: false);
        }

        public override void Back() => App.ShowMenu();
    }
}
