using System.Collections.Generic;
using System.Globalization;
using TenGo.Core;
using TenGo.Rendering;
using UnityEngine;

namespace TenGo.Screens
{
    /// Briques communes aux écrans de l'onglet Progression (profil, missions,
    /// succès, miroir, classement) : bouton retour rond, barre de progression,
    /// et les caractères des libellés iOS absents de Nunito (‹ › ✓ ↑ ↓ ✦) ou
    /// les SF Symbols (link, sparkles, star.fill, flame.fill, lock.fill),
    /// redessinés en traits ronds à la taille de police d'origine.
    public static class ProgressUi
    {
        static Vector2 P(float x, float y) => Units.P(x, y);

        // MARK: - Glyphes

        /// « › » (ou « ‹ » si `left`) d'une police de `fontPt` points.
        public static Transform Chevron(Transform parent, Vector2 posPt, float fontPt, Color color, int order, bool left = false)
        {
            var node = RenderShared.Node(left ? "ChevronLeft" : "Chevron", parent, P(posPt.x, posPt.y));
            float h = fontPt * 0.20f, w = fontPt * 0.13f;
            float dir = left ? -1 : 1;
            StrokeView.Create("Stroke", node, new[] { P(-w * dir, h), P(w * dir, 0), P(-w * dir, -h) }, color, fontPt * 0.075f, order);
            return node;
        }

        /// « ✓ » d'une police de `fontPt` points.
        public static Transform Check(Transform parent, Vector2 posPt, float fontPt, Color color, int order)
        {
            var node = RenderShared.Node("Check", parent, P(posPt.x, posPt.y));
            float s = fontPt / 30f;
            StrokeView.Create("Stroke", node, new[] { P(-8 * s, 0), P(-2.5f * s, -6 * s), P(9 * s, 7.5f * s) }, color, 3.4f * s, order);
            return node;
        }

        /// « ↑ » / « ↓ » d'une police de `fontPt` points ; rend la largeur occupée (points).
        public static float Arrow(Transform parent, Vector2 posPt, float fontPt, Color color, bool up, int order)
        {
            var node = RenderShared.Node(up ? "ArrowUp" : "ArrowDown", parent, P(posPt.x, posPt.y));
            float h = fontPt * 0.34f, head = fontPt * 0.2f, d = up ? 1 : -1;
            float w = fontPt * 0.09f;
            StrokeView.Create("Shaft", node, new[] { P(0, -h * d), P(0, h * d) }, color, w, order);
            StrokeView.Create("Head", node, new[] { P(-head, (h - head) * d), P(0, h * d), P(head, (h - head) * d) }, color, w, order);
            return head * 2 + fontPt * 0.4f;
        }

        /// « ✦ » : étoile à quatre branches pleine.
        public static Transform Sparkle(Transform parent, Vector2 posPt, float sizePt, Color color, int order)
        {
            var node = RenderShared.Node("Sparkle", parent, P(posPt.x, posPt.y));
            SparkleShape(node, Vector2.zero, sizePt * 0.42f, color, order);
            return node;
        }

        static void SparkleShape(Transform node, Vector2 c, float r, Color color, int order)
        {
            // Quatre losanges effilés + cœur : une étoile à branches concaves.
            for (int i = 0; i < 4; i++)
            {
                float a = i * 90f;
                var ray = ShapeView.Ellipse("Ray", node, P(c.x, c.y), new Vector2(r * 0.42f, r * 2f), color, default, 0, order);
                ray.transform.localRotation = Quaternion.Euler(0, 0, a);
            }
            ShapeView.Circle("Core", node, P(c.x, c.y), r * 0.32f, color, default, 0, order);
        }

        /// Icône de catégorie de succès (SF Symbol iOS), ou cadenas si `locked`.
        public static Transform CategoryIcon(Transform parent, Vector2 posPt, AchievementCategory category, float sizePt, Color color,
            int order, bool locked = false)
        {
            var node = RenderShared.Node("CategoryIcon", parent, P(posPt.x, posPt.y));
            float s = sizePt / 24f;
            if (locked)
            {
                // lock.fill : corps arrondi plein, anse en arc.
                ShapeView.Rect("Body", node, P(0, -3.5f * s), new Vector2(15 * s, 11.5f * s), 2.6f * s, color, default, 0, order);
                var arc = new List<Vector2>();
                for (int i = 0; i <= 12; i++)
                {
                    float a = Mathf.PI * i / 12f;
                    arc.Add(P(Mathf.Cos(a) * 4.6f * s, 2.2f * s + Mathf.Sin(a) * 5.2f * s));
                }
                arc.Insert(0, P(4.6f * s, -1 * s));
                arc.Add(P(-4.6f * s, -1 * s));
                StrokeView.Create("Shackle", node, arc, color, 2.4f * s, order);
                return node;
            }
            switch (category)
            {
                case AchievementCategory.Chain:
                    // link : deux maillons inclinés à 45°.
                    var links = RenderShared.Node("Links", node);
                    links.localRotation = Quaternion.Euler(0, 0, 45);
                    ShapeView.Rect("LinkA", links, P(-4.6f * s, 0), new Vector2(12.5f * s, 7.5f * s), 3.75f * s, Color.clear, color, 2.3f * s, order);
                    ShapeView.Rect("LinkB", links, P(4.6f * s, 0), new Vector2(12.5f * s, 7.5f * s), 3.75f * s, Color.clear, color, 2.3f * s, order);
                    break;
                case AchievementCategory.Perfect:
                    // sparkles : une grande étoile et deux petites.
                    SparkleShape(node, new Vector2(-1.5f * s, -1.5f * s), 8.5f * s, color, order);
                    SparkleShape(node, new Vector2(6.8f * s, 7f * s), 3.6f * s, color, order);
                    SparkleShape(node, new Vector2(7.5f * s, -6.5f * s), 2.6f * s, color, order);
                    break;
                case AchievementCategory.Score:
                    Glyphs.Star(node, Vector2.zero, sizePt * 1.15f, color, true, order);
                    break;
                default:
                    // flame.fill : flamme vectorielle du jeu.
                    VectorIcon.Create(node, VectorIconName.IconFlame, Vector2.zero, sizePt * 1.05f, color, order);
                    break;
            }
            return node;
        }

        // MARK: - Briques

        /// Bouton retour rond « ‹ » des écrans détaillés (rayon 36, relief 4).
        public static Transform BackButton(Transform parent, float yPt, int order = 30)
        {
            var back = RenderShared.Node("back", parent, P(0, yPt));
            var circle = ShapeView.Circle("Circle", back, Vector2.zero, 36, new Color(0.94f, 0.91f, 0.88f), Colors.White(0.68f, 0.45f), 1.5f, order);
            Relief.Raise(circle, 4);
            Chevron(back, Vector2.zero, 32, Colors.White(0.45f), order + 2, left: true);
            return back;
        }

        /// Barre de progression arrondie (hauteur `barH`), ancrée par son bord gauche.
        public static void Bar(Transform parent, float width, float ratio, Color track, Color fill, float leftX, float y, int order,
            float barH = 12f, bool minWidth = false)
        {
            ShapeView.Rect("Track", parent, P(leftX + width / 2, y), new Vector2(width, barH), barH / 2, track, default, 0, order);
            if (ratio <= 0 && !minWidth) return;
            float fillW = Mathf.Max(barH, width * ratio);
            ShapeView.Rect("Fill", parent, P(leftX + fillW / 2, y), new Vector2(fillW, barH), barH / 2, fill, default, 0, order + 1);
        }

        /// Libellé aligné à gauche, réduit s'il dépasse `maxW`.
        public static TenGoText Left(Transform parent, string text, NunitoWeight weight, float sizePt, Color color, float x, float y,
            int order, float maxW = 0)
        {
            var t = TenGoText.Create(parent, text, weight, sizePt, color, P(x, y), order, horizontal: TMPro.HorizontalAlignmentOptions.Left);
            if (maxW > 0) t.ShrinkToFit(maxW);
            return t;
        }

        public static TenGoText Right(Transform parent, string text, NunitoWeight weight, float sizePt, Color color, float x, float y,
            int order, float maxW = 0)
        {
            var t = TenGoText.Create(parent, text, weight, sizePt, color, P(x, y), order, horizontal: TMPro.HorizontalAlignmentOptions.Right);
            if (maxW > 0) t.ShrinkToFit(maxW);
            return t;
        }

        public static TenGoText Center(Transform parent, string text, NunitoWeight weight, float sizePt, Color color, float x, float y,
            int order, float maxW = 0)
        {
            var t = TenGoText.Create(parent, text, weight, sizePt, color, P(x, y), order);
            if (maxW > 0) t.ShrinkToFit(maxW);
            return t;
        }

        // MARK: - Textes

        /// Libellé d'une mission (`MissionDefinition.localizedTitle`).
        public static string MissionTitle(MissionDefinition def) => Loc.F(def.TitleKey, def.Target, def.Param);

        public static string AchievementTitle(AchievementDefinition def) => Loc.F(def.TitleKey, def.Target);
        public static string AchievementDescription(AchievementDefinition def) => Loc.F(def.DescKey, def.Target);

        static CultureInfo Culture
        {
            get
            {
                try { return CultureInfo.GetCultureInfo(Loc.Language); }
                catch (CultureNotFoundException) { return CultureInfo.InvariantCulture; }
            }
        }

        /// `CognitiveMirror.formatDecimal` : une décimale, séparateur de la langue.
        public static string FormatDecimal(double value) => value.ToString("0.0", Culture);

        public static string SkillId(MirrorSkill s) => s switch
        {
            MirrorSkill.Look => "look",
            MirrorSkill.Vision => "vision",
            MirrorSkill.Anticipation => "anticipation",
            _ => "repertoire",
        };

        /// `CognitiveMirror.displayValue`.
        public static string DisplayValue(MirrorSkill skill, double? value)
        {
            if (value is not double v) return "—";
            return skill switch
            {
                MirrorSkill.Look => Loc.F("mirror.unit.seconds", FormatDecimal(v)),
                MirrorSkill.Vision => FormatDecimal(v),
                MirrorSkill.Anticipation => (int)System.Math.Round(v * 100, System.MidpointRounding.AwayFromZero) + " %",
                _ => (int)v + " / " + Partition.Total,
            };
        }
    }
}
