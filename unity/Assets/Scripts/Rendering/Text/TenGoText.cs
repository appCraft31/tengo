using TenGo.Fx;
using TMPro;
using UnityEngine;

namespace TenGo.Rendering
{
    /// `SKLabelNode` : texte TextMeshPro en Nunito, taille en points, centré
    /// (`verticalAlignmentMode = .center`, sur la géométrie des glyphes) ou sur
    /// la ligne de base. Suit l'alpha hérité (`FxAlpha`). Les caractères absents
    /// de Nunito (japonais, coréen, chinois) passent par les replis Noto.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TextMeshPro))]
    public sealed class TenGoText : MonoBehaviour, IFxAlphaTarget
    {
        TextMeshPro _tmp;
        Color _color = Color.white;
        float _alpha = 1f;

        public TextMeshPro Tmp => _tmp != null ? _tmp : (_tmp = GetComponent<TextMeshPro>());

        public string Text { get => Tmp.text; set => Tmp.text = value ?? ""; }

        /// Couleur sRGB.
        public Color Color { get => _color; set { _color = value; OnInheritedAlphaChanged(); } }

        /// `label.alpha` propre au texte (multiplié par l'alpha hérité).
        public float Alpha { get => _alpha; set { _alpha = Mathf.Clamp01(value); OnInheritedAlphaChanged(); } }

        public float SizePt { get => Tmp.fontSize / Fonts.PtToTmp; set => Tmp.fontSize = value * Fonts.PtToTmp; }

        public static TenGoText Create(Transform parent, string text, NunitoWeight weight, float sizePt, Color color,
            Vector2 localPos, int sortingOrder, bool baseline = false,
            HorizontalAlignmentOptions horizontal = HorizontalAlignmentOptions.Center)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(TextMeshPro), typeof(TenGoText));
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var t = go.GetComponent<TenGoText>();
            var tmp = t.Tmp;
            tmp.font = Fonts.Get(weight);
            tmp.fontSize = sizePt * Fonts.PtToTmp;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.horizontalAlignment = horizontal;
            tmp.verticalAlignment = baseline ? VerticalAlignmentOptions.Baseline : VerticalAlignmentOptions.Geometry;
            tmp.rectTransform.sizeDelta = Vector2.zero;
            tmp.rectTransform.pivot = new Vector2(horizontal == HorizontalAlignmentOptions.Right ? 1f
                : horizontal == HorizontalAlignmentOptions.Left ? 0f : 0.5f, 0.5f);
            tmp.sortingOrder = sortingOrder;
            tmp.raycastTarget = false;
            tmp.isOrthographic = true;
            t._color = color;
            t.Text = text;
            t.OnInheritedAlphaChanged();
            return t;
        }

        public void OnInheritedAlphaChanged()
        {
            var c = _color;
            c.a *= _alpha * FxAlpha.Inherited(transform);
            Tmp.color = c;
        }
    }
}
