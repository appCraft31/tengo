using System.Collections.Generic;
using TenGo.Rendering;
using UnityEngine;

namespace TenGo.Screens
{
    /// Petits dessins des écrans de menu absents des polices (triangle plein,
    /// coche « ✓ », croix « × » épaisse), en traits ronds et formes SDF.
    public static class UiArt
    {
        /// Triangle plein (`SKShapeNode(path:)` fermé) : contours concentriques
        /// épais vers le centre de gravité. `pts` en unités locales.
        public static Transform FilledTriangle(Transform parent, IList<Vector2> pts, Color color, int order)
        {
            var node = RenderShared.Node("Triangle", parent);
            var c = (pts[0] + pts[1] + pts[2]) / 3f;
            float size = 0;
            foreach (var p in pts) size = Mathf.Max(size, (p - c).magnitude);
            // Rayon inscrit ≈ moitié du rayon circonscrit : trois anneaux épais le couvrent.
            float inPt = Units.ToPoints(size) * 0.5f;
            float widthPt = inPt * 0.7f;
            foreach (float k in new[] { 0.80f, 0.48f, 0.16f })
            {
                var ring = new List<Vector2>();
                foreach (var p in pts) ring.Add(c + (p - c) * Mathf.Max(0.05f, k));
                ring.Add(ring[0]);
                StrokeView.Create("Fill", node, ring, color, widthPt, order);
            }
            return node;
        }

        /// Coche « ✓ » au trait, `sizePt` = taille de police d'origine.
        public static Transform Check(Transform parent, Vector2 pos, float sizePt, Color color, int order)
        {
            var node = RenderShared.Node("Check", parent, pos);
            float s = sizePt / 20f;
            StrokeView.Create("Stroke", node, new[] { Units.P(-6 * s, 0), Units.P(-2 * s, -4.5f * s), Units.P(6.5f * s, 5 * s) },
                color, 2.2f * s, order);
            return node;
        }
    }
}
