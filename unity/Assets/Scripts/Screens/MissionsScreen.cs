using TenGo.App;
using TenGo.Core;
using TenGo.Haptics;
using TenGo.Fx;
using TenGo.Rendering;
using TenGo.Ui;
using UnityEngine;

namespace TenGo.Screens
{
    /// Missions du jour (MissionsScene.swift), refaites pour la 4.0
    /// (`design/refonte/05_profil`). Cibles : « claim_&lt;id&gt; », « back ».
    public sealed class MissionsScreen : ScreenBase
    {
        const int Z = 20;
        Transform _ui;
        float _cardWidth = 340;


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

        static float Pt(float v) => Design.Pt(v);

        /// Refonte 4.0 : retour en haut à gauche, cartes blanches empilées
        /// (titre, récompense, compteur, barre), « Récupérer » en bouton plein
        /// sous une mission terminée, super mission cerclée d'encre, missions
        /// réclamées estompées. La liste défile si besoin.
        void SetupUI()
        {
            _cardWidth = ContentW;
            AddBackHeader(Loc.T("missions.title"), null, () => App.ShowMenu(), _ui);
            Scroll = new ScrollArea(_ui, HeaderBottom, -HalfH + BottomInset, -UsableW / 2, UsableW / 2);
            float cursor = -Pt(10);
            foreach (var mission in S.Missions.TodaysMissions)
                cursor = AddMissionCard(mission, cursor) - Pt(12);
            Scroll.ContentHeight = -cursor + Pt(12);
            Scroll.ApplyClip();
        }

        /// Titre des écrans Missions et Succès (encre du système).
        internal static Color TitleColor => Design.Ink;

        float AddMissionCard(DisplayMission mission, float top)
        {
            var def = mission.Definition;
            float w = _cardWidth;
            bool claimable = mission.IsCompleted && !mission.Claimed;
            float kick = def.IsSuper ? Pt(22) : 0;
            float h = Pt(84) + kick + (claimable ? Pt(52) : 0);
            var card = RenderShared.Node("mission_" + def.Id, Scroll.Content, Units.P(0, top - h / 2));
            if (mission.Claimed) FxAlpha.Of(card.gameObject).Alpha = 0.55f;
            float r = Pt(Design.CardRadius);
            if (def.IsSuper)
                ShapeView.Rect("Ring", card, Vector2.zero, new Vector2(w + Pt(4), h + Pt(4)), r + Pt(2), Design.Ink, default, 0, Z - 1);
            Design.Surface("Bg", card, Vector2.zero, new Vector2(w, h), r, Z, hair: !def.IsSuper);

            float lx = -w / 2 + Pt(16), rx = w / 2 - Pt(16);
            float y = h / 2 - Pt(16);
            if (def.IsSuper)
            {
                VectorIcon.Create(card, VectorIconName.LineStar, Units.P(lx + Pt(7), y - Pt(4)), Pt(14), Design.Ink2, Z + 2);
                Design.Text(card, Loc.T("missions.super_badge"), NunitoWeight.Black, 11, Design.Ink2, new Vector2(lx + Pt(20), y - Pt(4)), Z + 2,
                    TMPro.HorizontalAlignmentOptions.Left);
                y -= kick;
            }
            float titleY = y - Pt(10);
            var reward = Design.Text(card, "+" + def.CoinReward, NunitoWeight.Black, 15, Design.Ink, new Vector2(rx, titleY), Z + 2,
                TMPro.HorizontalAlignmentOptions.Right);
            float rewardW = reward.WidthPt() + Pt(23);
            Design.Coin(card, new Vector2(rx - reward.WidthPt() - Pt(14), titleY), 18, Z + 2);
            Design.Text(card, ProgressUi.MissionTitle(def), NunitoWeight.ExtraBold, 15, Design.Ink, new Vector2(lx, titleY), Z + 2,
                TMPro.HorizontalAlignmentOptions.Left).ShrinkToFit(rx - rewardW - Pt(10) - lx);

            float subY = titleY - Pt(30);
            Design.Text(card, Design.Number(Mathf.Min(mission.Progress, def.Target)) + " / " + Design.Number(def.Target), NunitoWeight.ExtraBold, 12,
                Design.Ink3, new Vector2(lx, subY), Z + 2, TMPro.HorizontalAlignmentOptions.Left);
            if (mission.Claimed)
                Design.Text(card, Loc.T("ui.missions.claimed"), NunitoWeight.ExtraBold, 12, Design.Ink3, new Vector2(rx, subY), Z + 2,
                    TMPro.HorizontalAlignmentOptions.Right);
            float ratio = def.Target > 0 ? Mathf.Clamp01(mission.Progress / (float)def.Target) : 0;
            Design.Bar(card, new Vector2(lx, subY - Pt(18)), (rx - lx) / Design.Scale, 8, ratio, claimable ? Design.Accent : Design.Ink, Z + 2);

            if (claimable)
            {
                string id = def.Id;
                var btnPos = new Vector2(0, -h / 2 + Pt(16 + 20));
                var claim = RenderShared.Node("claim_" + id, card, Units.P(btnPos.x, btnPos.y));
                ShapeView.Rect("Bg", claim, Vector2.zero, new Vector2(rx - lx, Pt(40)), Pt(14), Design.Accent, default, 0, Z + 3);
                var label = Design.Text(claim, Loc.T("missions.claim") + "  +" + def.CoinReward, NunitoWeight.Black, 15, Design.OnAccent,
                    Vector2.zero, Z + 5, TMPro.HorizontalAlignmentOptions.Left);
                float total = Pt(18 + 6) + label.WidthPt() + Pt(22);
                VectorIcon.Create(claim, VectorIconName.LineCheck, Units.P(-total / 2 + Pt(9), 0), Pt(18), Design.OnAccent, Z + 5);
                label.transform.localPosition = Units.P(-total / 2 + Pt(24), 0);
                Design.Coin(claim, new Vector2(-total / 2 + Pt(24) + label.WidthPt() + Pt(14), 0), 16, Z + 5);
                AddTarget("claim_" + id, claim, new Vector2(rx - lx, Pt(40)), () => Claim(id), animate: false, haptic: false);
            }
            return top - h;
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
