using UnityEngine;
using TMPro;

namespace TenGo.Rendering
{
    /// Mesures et mises en forme des libellés (`frame.width`, `setScale`,
    /// `numberOfLines` / `preferredMaxLayoutWidth` des `SKLabelNode`).
    public static class TextMetrics
    {
        /// Largeur du texte en points (échelle propre non comprise).
        public static float WidthPt(this TenGoText t)
        {
            var v = t.Tmp.GetPreferredValues(t.Text);
            return v.x * Units.PointsPerUnit;
        }

        /// Hauteur du texte en points (lignes comprises si le texte est replié).
        public static float HeightPt(this TenGoText t)
        {
            t.Tmp.ForceMeshUpdate();
            return t.Tmp.textBounds.size.y * Units.PointsPerUnit;
        }

        /// Réduit le libellé s'il dépasse `maxWidthPt` (`if frame.width > max { setScale }`).
        public static TenGoText ShrinkToFit(this TenGoText t, float maxWidthPt)
        {
            t.transform.localScale = Vector3.one;
            float w = t.WidthPt();
            if (w > maxWidthPt && w > 0) t.transform.localScale = Vector3.one * (maxWidthPt / w);
            return t;
        }

        /// Texte replié sur `maxWidthPt` (`numberOfLines = 0`, `preferredMaxLayoutWidth`), centré.
        public static TenGoText Wrap(this TenGoText t, float maxWidthPt)
        {
            var tmp = t.Tmp;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.rectTransform.sizeDelta = new Vector2(maxWidthPt / Units.PointsPerUnit, 0f);
            // Une ligne plus courte que la largeur reste centrée sur son point d'ancrage.
            float natural = t.WidthPt();
            if (natural < maxWidthPt) tmp.rectTransform.sizeDelta = new Vector2(natural / Units.PointsPerUnit + 0.01f, 0f);
            tmp.verticalAlignment = VerticalAlignmentOptions.Middle;
            return t;
        }
    }
}
