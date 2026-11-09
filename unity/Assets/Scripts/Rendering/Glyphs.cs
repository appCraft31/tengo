using System.Collections.Generic;
using UnityEngine;

namespace TenGo.Rendering
{
    /// Caractères des libellés iOS absents de Nunito et des replis CJK (★/☆),
    /// redessinés en traits ronds (`StrokeView`) à la taille de police
    /// d'origine. `sizePt` = `fontSize` du `SKLabelNode`. Refonte 4.0 : ⌂, ↺ et
    /// l'engrenage sont remplacés par les icônes au trait (`VectorIconName.Line…`).
    public static class Glyphs
    {
        static Vector2 P(float x, float y) => Units.P(x, y);

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
    }
}
