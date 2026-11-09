using System.Collections.Generic;
using UnityEngine;

namespace TenGo.Rendering
{
    /// Caractères des libellés iOS absents de Nunito et des replis CJK
    /// (⌂ U+2302, ↺ U+21BA, ★/☆), redessinés en traits ronds (`StrokeView`) à la
    /// taille de police d'origine. `sizePt` = `fontSize` du `SKLabelNode`.
    public static class Glyphs
    {
        static Vector2 P(float x, float y) => Units.P(x, y);

        /// « ⌂ » : maison au trait (toit pointu, murs, sol).
        public static Transform Home(Transform parent, Vector2 pos, float sizePt, Color color, int order)
        {
            var node = RenderShared.Node("GlyphHome", parent, pos);
            float s = sizePt / 28f;
            var pts = new List<Vector2> { P(-7 * s, -6.5f * s), P(-7 * s, 1.5f * s), P(0, 8 * s), P(7 * s, 1.5f * s), P(7 * s, -6.5f * s), P(-7 * s, -6.5f * s) };
            StrokeView.Create("Stroke", node, pts, color, 1.9f * s, order);
            return node;
        }

        /// « ↺ » : arc ouvert en haut à gauche et pointe de flèche (sens antihoraire).
        public static Transform Restart(Transform parent, Vector2 pos, float sizePt, Color color, int order, float weight = 1f)
        {
            var node = RenderShared.Node("GlyphRestart", parent, pos);
            float s = sizePt / 30f;
            float r = 7.2f * s;
            var arc = new List<Vector2>();
            // De 120° à 120° − 300° (sens horaire de dessin), l'ouverture en haut à gauche.
            for (int i = 0; i <= 24; i++)
            {
                float a = Mathf.Deg2Rad * (115f - 290f * i / 24f);
                arc.Add(P(Mathf.Cos(a) * r, Mathf.Sin(a) * r - 0.5f * s));
            }
            float w = 2.0f * s * weight;
            StrokeView.Create("Arc", node, arc, color, w, order);
            // Pointe au départ de l'arc (115°), tournée dans le sens antihoraire.
            var tip = arc[0];
            float a0 = Mathf.Deg2Rad * 115f;
            var tangent = new Vector2(-Mathf.Sin(a0), Mathf.Cos(a0));   // antihoraire
            var normal = new Vector2(Mathf.Cos(a0), Mathf.Sin(a0));
            float h = Units.FromPoints(3.6f * s);
            var head = tip + tangent * h * 0.6f;
            StrokeView.Create("Head", node, new[] { head - tangent * h + normal * h, head, head - tangent * h - normal * h }, color, w, order);
            return node;
        }

        /// « ★ » (plein) ou « ☆ » (contour), étoile à cinq branches.
        public static Transform Star(Transform parent, Vector2 pos, float sizePt, Color color, bool filled, int order)
        {
            var node = RenderShared.Node(filled ? "StarFull" : "StarEmpty", parent, pos);
            float outer = sizePt * 0.40f, inner = outer * 0.45f;
            var pts = new List<Vector2>();
            for (int i = 0; i <= 10; i++)
            {
                float a = Mathf.PI / 2f + i * Mathf.PI / 5f;
                float r = i % 2 == 0 ? outer : inner;
                pts.Add(P(Mathf.Cos(a) * r, Mathf.Sin(a) * r));
            }
            if (filled)
            {
                // Remplissage : contour épais rentré, puis cœur plein.
                StrokeView.Create("Fill", node, pts, color, sizePt * 0.13f, order);
                ShapeView.Circle("Core", node, Vector2.zero, inner * 1.05f, color, default, 0, order);
                var mid = new List<Vector2>();
                foreach (var p in pts) mid.Add(p * 0.62f);
                StrokeView.Create("Fill2", node, mid, color, sizePt * 0.22f, order);
            }
            else
            {
                StrokeView.Create("Outline", node, pts, color, sizePt * 0.07f, order);
            }
            return node;
        }

        /// « gearshape.fill » (SF Symbol) : roue dentée pleine, moyeu évidé de `hole`.
        public static Transform Gear(Transform parent, Vector2 pos, float sizePt, Color color, Color hole, int order)
        {
            var node = RenderShared.Node("GlyphGear", parent, pos);
            float s = sizePt / 24f;
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4f;
                var tooth = ShapeView.Rect("Tooth", node, P(Mathf.Cos(a) * 9.2f * s, Mathf.Sin(a) * 9.2f * s), new Vector2(5.2f * s, 5.6f * s),
                    1.4f * s, color, default, 0, order);
                tooth.transform.localRotation = Quaternion.Euler(0, 0, a * Mathf.Rad2Deg);
            }
            ShapeView.Circle("Body", node, Vector2.zero, 8.6f * s, color, default, 0, order);
            ShapeView.Circle("Hole", node, Vector2.zero, 3.4f * s, hole, default, 0, order + 1);
            return node;
        }
    }
}
