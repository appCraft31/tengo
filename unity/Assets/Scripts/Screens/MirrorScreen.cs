using System.Linq;
using TenGo.App;
using TenGo.Core;
using TenGo.Rendering;
using TenGo.Ui;
using UnityEngine;

namespace TenGo.Screens
{
    /// « Ton cerveau » (MirrorScene.swift) : les quatre aptitudes du miroir
    /// cognitif comparées à la semaine précédente, puis le carnet des 41 façons
    /// de faire 10 (`design/refonte/05_profil`). Le contenu défile. Cible : « back ».
    public sealed class MirrorScreen : ScreenBase
    {
        const int Z = 20;
        static readonly MirrorSkill[] Skills = { MirrorSkill.Look, MirrorSkill.Vision, MirrorSkill.Anticipation, MirrorSkill.Repertoire };
        float _cardWidth = 340;

        /// Le défilement du contenu (tests).
        public ScrollArea Content => Scroll;

        public MirrorScreen(AppRoot app) : base(app, "MirrorScreen")
        {
            TapOnRelease = true;
            SetupUI();
            S.Analytics.MirrorOpened(S.Mirror.GamesMeasured, S.Mirror.RepertoireCount);
        }

        static float Pt(float v) => Design.Pt(v);

        /// Refonte 4.0 : retour en haut à gauche, sous-titre, quatre mesures en
        /// cartes blanches (la pastille colorée sert de repère), carnet des 10 en
        /// rangées de points par longueur. Le tout défile.
        void SetupUI()
        {
            _cardWidth = ContentW;
            AddBackHeader(Loc.T("mirror.title"), null, () => App.ShowProfile());
            Scroll = new ScrollArea(Root, HeaderBottom, -HalfH + BottomInset, -UsableW / 2, UsableW / 2);
            var content = Scroll.Content;
            float left = -_cardWidth / 2;
            float cursor = -Pt(10);

            var mirror = S.Mirror;
            string subtitle = mirror.GamesMeasured == 0 ? Loc.T("mirror.calibrating") : Loc.F("mirror.subtitle", mirror.GamesMeasured);
            var sub = Design.Text(content, subtitle, NunitoWeight.Bold, 14, Design.Ink2, new Vector2(left, cursor), Z,
                TMPro.HorizontalAlignmentOptions.Left).Wrap(_cardWidth);
            sub.transform.localPosition = Units.P(left, cursor - sub.HeightPt() / 2);
            cursor -= sub.HeightPt() + Pt(14);

            float gap = Pt(10), cardH = Pt(136);
            float cardW = (_cardWidth - gap) / 2;
            for (int i = 0; i < Skills.Length; i++)
            {
                int column = i % 2, row = i / 2;
                var center = new Vector2(left + cardW / 2 + column * (cardW + gap), cursor - cardH / 2 - row * (cardH + gap));
                AddSkillCard(Skills[i], i, center, new Vector2(cardW, cardH), content);
            }
            cursor -= 2 * cardH + gap + Pt(22);
            cursor = AddNotebook(content, cursor);

            Scroll.ContentHeight = -cursor + Pt(24);
            Scroll.ApplyClip();
        }

        static readonly int[] SkillTints = { 3, 7, 9, 8 };

        void AddSkillCard(MirrorSkill skill, int index, Vector2 center, Vector2 size, Transform parent)
        {
            var reading = S.Mirror.WeeklyReading(skill);
            string id = ProgressUi.SkillId(skill);
            var card = RenderShared.Node("Skill_" + id, parent, Units.P(center.x, center.y));
            Design.Surface("Bg", card, Vector2.zero, size, Pt(Design.CardRadius), Z);
            float lx = -size.x / 2 + Pt(14), maxW = size.x - Pt(28);
            float top = size.y / 2;
            ShapeView.Circle("Dot", card, Units.P(lx + Pt(5), top - Pt(24)), Pt(5), Look.BubbleColor(SkillTints[index]), default, 0, Z + 1);
            Design.Text(card, Loc.T("mirror.skill." + id), NunitoWeight.ExtraBold, 13, Design.Ink2, new Vector2(lx + Pt(18), top - Pt(24)), Z + 2,
                TMPro.HorizontalAlignmentOptions.Left).ShrinkToFit(maxW - Pt(18));
            Design.Text(card, ProgressUi.DisplayValue(skill, reading.Value), NunitoWeight.Black, 30, Design.Ink, new Vector2(lx, top - Pt(58)), Z + 2,
                TMPro.HorizontalAlignmentOptions.Left).ShrinkToFit(maxW);
            var (text, positive, arrow) = Trend(skill, reading);
            var trendColor = positive ? Design.SeedGreen : Design.Ink3;
            float textX = lx;
            float ty = top - Pt(90);
            if (arrow != 0)
                textX += ProgressUi.Arrow(card, new Vector2(lx + Pt(12) * 0.22f, ty), Pt(12), trendColor, arrow > 0, Z + 2);
            Design.Text(card, text, NunitoWeight.Black, 12, trendColor, new Vector2(textX, ty), Z + 2, TMPro.HorizontalAlignmentOptions.Left)
                .ShrinkToFit(maxW - (textX - lx));
            Design.Text(card, Loc.T("mirror.skill." + id + ".explain"), NunitoWeight.Bold, 11, Design.Ink3, new Vector2(lx, top - Pt(110)), Z + 2,
                TMPro.HorizontalAlignmentOptions.Left).ShrinkToFit(maxW);
        }

        /// Évolution lisible (`trendText`) ; la flèche « ↑ / ↓ » est redessinée (absente de Nunito).
        static (string text, bool positive, int arrow) Trend(MirrorSkill skill, MirrorReading reading)
        {
            if (skill == MirrorSkill.Repertoire)
            {
                int gained = (int)(reading.Value ?? 0) - (int)(reading.Previous ?? 0);
                return gained > 0 ? (Loc.F("mirror.trend.new_week", gained), true, 1) : (Loc.T("mirror.trend.stable"), false, 0);
            }
            if (reading.Value is not double value || reading.Previous is not double previous)
                return (Loc.T("mirror.trend.none"), false, 0);
            double improvement = reading.Improvement(Mirror.LowerIsBetter(skill)) ?? 0;
            if (System.Math.Abs(improvement) < 0.03) return (Loc.T("mirror.trend.stable"), false, 0);
            bool better = improvement > 0;
            double delta = System.Math.Abs(value - previous);
            string text = skill switch
            {
                MirrorSkill.Look => Loc.F(better ? "mirror.trend.faster" : "mirror.trend.slower", ProgressUi.FormatDecimal(delta)),
                MirrorSkill.Vision => Loc.F(better ? "mirror.trend.more" : "mirror.trend.less", ProgressUi.FormatDecimal(delta)),
                _ => Loc.F(better ? "mirror.trend.points_up" : "mirror.trend.points_down",
                    (int)System.Math.Round(delta * 100, System.MidpointRounding.AwayFromZero)),
            };
            return (text, better, better ? 1 : -1);
        }

        /// Carnet des 10 : une rangée par longueur, un point par décomposition (plein = trouvée).
        float AddNotebook(Transform parent, float top)
        {
            var mirror = S.Mirror;
            var discovered = mirror.Data.Discovered;
            float left = -_cardWidth / 2;
            float cursor = top;
            var head = RenderShared.Node("notebook", parent, Units.P(0, cursor));
            Design.Text(head, Loc.T("mirror.notebook"), NunitoWeight.Black, 17, Design.Ink, new Vector2(left, 0), Z + 2,
                TMPro.HorizontalAlignmentOptions.Left);
            Design.Text(head, mirror.RepertoireCount + " / " + Partition.Total, NunitoWeight.ExtraBold, 13, Design.Ink3,
                new Vector2(-left, 0), Z + 2, TMPro.HorizontalAlignmentOptions.Right);
            cursor -= Pt(22);

            var groups = Partition.All.GroupBy(p => p.Count).OrderBy(g => g.Key).ToList();
            float rowH = Pt(39), padY = Pt(6);
            float cardH = groups.Count * rowH + 2 * padY;
            var card = RenderShared.Node("NotebookCard", parent, Units.P(0, cursor - cardH / 2));
            Design.Surface("Bg", card, Vector2.zero, new Vector2(_cardWidth, cardH), Pt(Design.CardRadius), Z);
            float lx = left + Pt(14), rx = -left - Pt(14);
            float dotsX = lx + Pt(72), dot = Pt(12), dotGap = Pt(4);
            for (int i = 0; i < groups.Count; i++)
            {
                var parts = groups[i].ToList();
                int found = parts.Count(p => discovered.ContainsKey(Partition.Key(p)));
                float y = cardH / 2 - padY - rowH * (i + 0.5f);
                if (i > 0) ShapeView.Rect("Sep", card, Units.P(0, y + rowH / 2), new Vector2(_cardWidth - Pt(28), Pt(1)), 0, Design.Line, default, 0, Z + 1);
                Design.Text(card, Loc.F("mirror.notebook.group", groups[i].Key), NunitoWeight.ExtraBold, 12, Design.Ink2, new Vector2(lx, y), Z + 2,
                    TMPro.HorizontalAlignmentOptions.Left).ShrinkToFit(Pt(66));
                // Trouvées d'abord, à gauche.
                for (int k = 0; k < parts.Count; k++)
                    ShapeView.Circle("Dot", card, Units.P(dotsX + dot / 2 + k * (dot + dotGap), y), dot / 2, k < found ? Design.Accent : Design.Sunk,
                        default, 0, Z + 2);
                Design.Text(card, found + " / " + parts.Count, NunitoWeight.Black, 12, Design.Ink, new Vector2(rx, y), Z + 2,
                    TMPro.HorizontalAlignmentOptions.Right);
            }
            return cursor - cardH;
        }

        public override void Back() => App.ShowProfile();
    }
}
