using TenGo.App;
using TenGo.Core;
using TenGo.Haptics;
using TenGo.Rendering;
using UnityEngine;

namespace TenGo.Screens
{
    /// Missions du jour (MissionsScene.swift) : quatre cartes qui remplissent la
    /// bande disponible, progression, récompense ou bouton « Réclamer », coche
    /// une fois réclamée. Cibles : « claim_&lt;id&gt; », « back ».
    public sealed class MissionsScreen : ScreenBase
    {
        const int Z = 20;
        Transform _ui;
        float _cardWidth = 340;

        static Vector2 P(float x, float y) => Units.P(x, y);

        public MissionsScreen(AppRoot app) : base(app, "MissionsScreen")
        {
            Rebuild();
        }

        public void Rebuild()
        {
            if (_ui != null) RenderShared.Destroy(_ui.gameObject);
            Targets.Clear();
            _ui = RenderShared.Node("MissionsUI", Root);
            SetupUI();
        }

        void SetupUI()
        {
            float topY = SceneSize.y / 2;
            float bottomY = -HalfH + Geo.SafeBottomPx / Geo.PixelsPerScene;
            _cardWidth = Mathf.Min(UsableW - 48, 600);

            // Titre gris foncé d'iOS (white 0.28) ; le logo du thème le remplace pour rester lisible de nuit.
            TenGoText.Create(_ui, Loc.T("missions.title"), Fonts.AvenirHeavy, 40, TitleColor, P(0, topY - 110), Z)
                .ShrinkToFit(UsableW - 40);

            var missions = S.Missions.TodaysMissions;
            float listTop = topY - 180;
            float listBottom = bottomY + 150;
            float available = Mathf.Max(0, listTop - listBottom);
            const float gap = 18;
            int count = Mathf.Max(1, missions.Count);
            float rowH = Mathf.Max(96, Mathf.Min(150, available / count - gap));
            float blockH = rowH * count + gap * (count - 1);
            float cursorY = listTop - Mathf.Max(0, (available - blockH) / 2) - rowH / 2;
            foreach (var mission in missions)
            {
                AddMissionRow(mission, cursorY, rowH);
                cursorY -= rowH + gap;
            }

            var back = ProgressUi.BackButton(_ui, bottomY + 90, Z + 10);
            AddTarget("back", back, new Vector2(80, 80), () => App.ShowMenu());
        }

        /// Titre : gris foncé d'iOS sur fond clair, encre du thème sur fond sombre.
        internal static Color TitleColor =>
            Look.Background.PerceivedLuminance() > 0.5f ? Colors.White(0.28f) : Look.Logo;

        void AddMissionRow(DisplayMission mission, float y, float height)
        {
            var def = mission.Definition;
            float width = _cardWidth;
            var row = RenderShared.Node("mission_" + def.Id, _ui, P(0, y));
            var bg = ShapeView.Rect("Bg", row, Vector2.zero, new Vector2(width, height), 22,
                def.IsSuper ? new Color(0.94f, 0.88f, 0.98f) : new Color(0.97f, 0.95f, 0.92f), Colors.White(0.70f, 0.35f), 1, Z);
            Relief.Raise(bg, 4);

            const float rightColumnW = 150;
            float textLeft = -width / 2 + 22;
            float titleY = height * 0.14f;

            var title = TenGoText.Create(row, ProgressUi.MissionTitle(def), Fonts.AvenirDemiBold, 19, Colors.White(0.26f),
                P(textLeft, titleY), Z + 2, horizontal: TMPro.HorizontalAlignmentOptions.Left);
            title.Wrap(width - rightColumnW - 44);

            ProgressUi.Left(row, mission.Progress + " / " + def.Target, Fonts.AvenirMedium, 14, Colors.White(0.45f),
                textLeft, -height * 0.16f, Z + 2);

            float barW = width - 44;
            float ratio = def.Target > 0 ? Mathf.Clamp01(mission.Progress / (float)def.Target) : 0;
            ProgressUi.Bar(row, barW, ratio, Colors.White(0.85f),
                mission.IsCompleted ? new Color(0.42f, 0.72f, 0.48f) : Look.Accent, -barW / 2, -height * 0.34f, Z + 2, 10, minWidth: true);

            if (mission.Claimed)
            {
                ProgressUi.Check(row, new Vector2(width / 2 - 52, titleY), 32, new Color(0.45f, 0.62f, 0.48f), Z + 2);
            }
            else if (mission.IsCompleted)
            {
                var claim = RenderShared.Node("claim_" + def.Id, row, P(width / 2 - 76, titleY));
                var cbg = ShapeView.Rect("Bg", claim, Vector2.zero, new Vector2(124, 52), 26, new Color(0.98f, 0.80f, 0.35f),
                    Colors.White(0.60f, 0.35f), 1, Z + 4);
                Relief.Raise(cbg, 4);
                TenGoText.Create(claim, Loc.T("missions.claim"), Fonts.AvenirBold, 17, Colors.White(0.2f), Vector2.zero, Z + 6)
                    .ShrinkToFit(112);
                string id = def.Id;
                AddTarget("claim_" + def.Id, claim, new Vector2(124, 52), () => Claim(id), animate: false, haptic: false);
            }
            else
            {
                CoinIcon.Make(row, P(width / 2 - 92, titleY), 12, Z + 2);
                ProgressUi.Left(row, "+" + def.CoinReward, Fonts.AvenirDemiBold, 19, Colors.White(0.35f), width / 2 - 74, titleY, Z + 2);
            }

            if (def.IsSuper)
                ProgressUi.Left(row, Loc.T("missions.super_badge"), Fonts.AvenirBold, 11, new Color(0.55f, 0.35f, 0.75f),
                    -width / 2 + 20, height / 2 - 11, Z + 2);
        }

        /// Réclame une mission ; vrai si des pièces ont été créditées.
        public bool Claim(string id)
        {
            if (S.Missions.Claim(id) == null) return false;
            HapticManager.Medium();
            App.FlushSave();
            App.Show(App.Screens.Missions(App));
            return true;
        }

        public override void Back() => App.ShowMenu();
    }
}
