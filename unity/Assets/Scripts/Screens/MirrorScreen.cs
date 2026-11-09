using System.Linq;
using TenGo.App;
using TenGo.Core;
using TenGo.Rendering;
using UnityEngine;

namespace TenGo.Screens
{
    /// « Ton cerveau » (MirrorScene.swift) : les quatre aptitudes du miroir
    /// cognitif comparées à la semaine précédente, puis le carnet des 41 façons
    /// de faire 10. iOS tasse le contenu pour qu'il tienne ; ici il défile
    /// (même mise en page à l'échelle 1). Cible : « back ».
    public sealed class MirrorScreen : ScreenBase
    {
        const int Z = 20;
        static readonly MirrorSkill[] Skills = { MirrorSkill.Look, MirrorSkill.Vision, MirrorSkill.Anticipation, MirrorSkill.Repertoire };
        float _cardWidth = 340;

        static Vector2 P(float x, float y) => Units.P(x, y);
        /// Le défilement du contenu (tests).
        public ScrollArea Content => Scroll;

        public MirrorScreen(AppRoot app) : base(app, "MirrorScreen")
        {
            TapOnRelease = true;
            SetupUI();
            S.Analytics.MirrorOpened(S.Mirror.GamesMeasured, S.Mirror.RepertoireCount);
        }

        void SetupUI()
        {
            var logo = Look.Logo;
            float halfH = HalfH;
            float safeTop = Mathf.Max(TopInset, 47);
            float bottomY = -halfH + Geo.SafeBottomPx / Geo.PixelsPerScene;
            _cardWidth = Mathf.Min(UsableW - 48, 600);

            float titleY = halfH - safeTop - 36;
            TenGoText.Create(Root, Loc.T("mirror.title"), Fonts.AvenirHeavy, 36, logo, P(0, titleY), Z).ShrinkToFit(UsableW - 40);

            var mirror = S.Mirror;
            string subtitle = mirror.GamesMeasured == 0 ? Loc.T("mirror.calibrating") : Loc.F("mirror.subtitle", mirror.GamesMeasured);
            var sub = TenGoText.Create(Root, subtitle, Fonts.AvenirMedium, 15, logo.WithAlpha(0.7f), P(0, titleY - 40), Z);
            sub.Wrap(_cardWidth);

            var back = ProgressUi.BackButton(Root, bottomY + 70, Z + 10);
            AddTarget("back", back, new Vector2(80, 80), () => App.ShowProfile());

            float top = titleY - 76;
            float bottom = bottomY + 120;
            const float pad = 10;
            Scroll = new ScrollArea(Root, top + pad, bottom - pad, -UsableW / 2, UsableW / 2);
            var content = Scroll.Content;
            float cursor = -pad;

            const float gap = 14, cardH = 156;
            float cardW = (_cardWidth - gap) / 2;
            for (int i = 0; i < Skills.Length; i++)
            {
                int column = i % 2, row = i / 2;
                var center = new Vector2((column == 0 ? -1 : 1) * (cardW + gap) / 2, cursor - cardH / 2 - row * (cardH + gap));
                AddSkillCard(Skills[i], i, center, new Vector2(cardW, cardH), content);
            }
            cursor -= 2 * cardH + gap + 34;
            cursor = AddNotebook(content, cursor);

            float height = -cursor + pad;
            float available = Scroll.Top - Scroll.Bottom;
            Scroll.ContentHeight = height;
            // Contenu plus court que la zone : centré verticalement, comme iOS.
            if (height < available)
                foreach (Transform child in content) child.localPosition += (Vector3)P(0, -(available - height) / 2);
            Scroll.ApplyClip();
        }

        void AddSkillCard(MirrorSkill skill, int index, Vector2 center, Vector2 size, Transform parent)
        {
            var reading = S.Mirror.WeeklyReading(skill);
            var tint = Look.BubbleColor(new[] { 3, 5, 7, 9 }[index]);
            var fill = tint.MixedWithWhite(0.55f);
            var ink = fill.ReadableInk();

            var card = ShapeView.Rect("Skill_" + ProgressUi.SkillId(skill), parent, P(center.x, center.y), size, 24, fill, default, 0, Z);
            Relief.Raise(card, 4);

            float left = center.x - size.x / 2 + 20;
            float maxW = size.x - 36;
            ProgressUi.Left(parent, Loc.T("mirror.skill." + ProgressUi.SkillId(skill)), Fonts.AvenirBold, 18, ink, left, center.y + 50, Z + 2, maxW);
            ProgressUi.Left(parent, ProgressUi.DisplayValue(skill, reading.Value), Fonts.AvenirHeavy, 34, ink, left, center.y + 12, Z + 2, maxW);
            var (text, positive, arrow) = Trend(skill, reading);
            var trendColor = ink.WithAlpha(positive ? 1 : 0.62f);
            float textX = left;
            if (arrow != 0)
                textX += ProgressUi.Arrow(parent, new Vector2(left + 15 * 0.22f, center.y - 26), 15, trendColor, arrow > 0, Z + 2);
            ProgressUi.Left(parent, text, Fonts.AvenirDemiBold, 15, trendColor, textX, center.y - 26, Z + 2, maxW - (textX - left));
            ProgressUi.Left(parent, Loc.T("mirror.skill." + ProgressUi.SkillId(skill) + ".explain"), Fonts.AvenirMedium, 13,
                ink.WithAlpha(0.72f), left, center.y - 54, Z + 2, maxW);
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

        /// Carnet des 10 : les 41 décompositions, groupées par nombre de bulles.
        float AddNotebook(Transform parent, float top)
        {
            var mirror = S.Mirror;
            var discovered = mirror.Data.Discovered;
            var logo = Look.Logo;
            float cursor = top;

            ProgressUi.Left(parent, Loc.T("mirror.notebook"), Fonts.AvenirBold, 22, logo, -_cardWidth / 2 + 4, cursor, Z + 2);
            ProgressUi.Right(parent, mirror.RepertoireCount + " / " + Partition.Total, Fonts.AvenirDemiBold, 18, logo.WithAlpha(0.7f),
                _cardWidth / 2 - 4, cursor, Z + 2);
            cursor -= 36;

            const float chipH = 30, chipGap = 8, labelW = 92;
            float flowLeft = -_cardWidth / 2 + labelW;
            float flowRight = _cardWidth / 2;
            var accent = Look.Accent;
            var foundFill = accent.MixedWithWhite(0.35f);
            var foundInk = foundFill.ReadableInk();

            foreach (var group in Partition.All.GroupBy(p => p.Count).OrderBy(g => g.Key))
            {
                var parts = group.ToList();
                int found = parts.Count(p => discovered.ContainsKey(Partition.Key(p)));
                ProgressUi.Left(parent, Loc.F("mirror.notebook.group", group.Key), Fonts.AvenirDemiBold, 14,
                    logo.WithAlpha(found == parts.Count ? 1 : 0.7f), -_cardWidth / 2 + 4, cursor - chipH / 2, Z + 2, labelW - 10);

                float x = flowLeft;
                float rowY = cursor - chipH / 2;
                foreach (var part in parts)
                {
                    bool isFound = discovered.ContainsKey(Partition.Key(part));
                    string text = isFound ? string.Join("+", part) : string.Join("+", Enumerable.Repeat("?", part.Count));
                    var label = TenGoText.Create(parent, text, isFound ? Fonts.AvenirBold : Fonts.AvenirMedium, 14,
                        isFound ? foundInk : logo.WithAlpha(0.4f), Vector2.zero, Z + 4);
                    float width = label.WidthPt() + 18;
                    if (x + width > flowRight)
                    {
                        x = flowLeft;
                        rowY -= chipH + chipGap;
                    }
                    var pos = new Vector2(x + width / 2, rowY);
                    ShapeView.Rect("Chip", parent, P(pos.x, pos.y), new Vector2(width, chipH), chipH / 2,
                        isFound ? foundFill : Colors.White(1, 0.45f), isFound ? default : logo.WithAlpha(0.18f), isFound ? 0 : 1, Z + 2);
                    label.transform.localPosition = P(pos.x, pos.y);
                    x += width + chipGap;
                }
                cursor = rowY - chipH / 2 - 14;
            }
            return cursor;
        }

        public override void Back() => App.ShowProfile();
    }
}
