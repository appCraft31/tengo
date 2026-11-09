using TenGo.Fx;
using UnityEngine;

namespace TenGo.Rendering
{
    /// Icône vectorielle teintée (`VectorIcon.node(size:color:)`) : `sizePt` de
    /// côté pour le `viewBox` 24 × 24, couleur sRGB, alpha hérité.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class VectorIcon : MonoBehaviour, IFxAlphaTarget
    {
        static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int SdfId = Shader.PropertyToID("_Sdf");

        VectorIconName _name;
        float _sizePt;
        Color _color;
        int _order;
        MaterialPropertyBlock _block;

        public VectorIconName Name => _name;
        public Color Color { get => _color; set { _color = value; Apply(); } }
        public float SizePt { get => _sizePt; set { _sizePt = value; Apply(); } }

        public static VectorIcon Create(Transform parent, VectorIconName name, Vector2 pos, float sizePt, Color color, int sortingOrder)
        {
            var go = new GameObject("Icon" + name, typeof(MeshFilter), typeof(MeshRenderer), typeof(VectorIcon));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            var v = go.GetComponent<VectorIcon>();
            v._name = name; v._sizePt = sizePt; v._color = color; v._order = sortingOrder;
            v.Apply();
            return v;
        }

        public void OnInheritedAlphaChanged() => Apply();

        void Apply()
        {
            var mat = RenderShared.Material("IconSdf");
            if (mat == null) return;
            var r = RenderShared.Setup(gameObject, RenderShared.Quad, mat);
            r.sortingOrder = _order;
            // Le champ couvre le viewBox plus une marge : le quad suit.
            float half = Units.FromPoints(_sizePt * 0.5f) * (IconLibrary.ViewBox + IconLibrary.Pad * 2f) / IconLibrary.ViewBox;
            _block ??= new MaterialPropertyBlock();
            _block.SetTexture(MainTexId, IconLibrary.Field(_name));
            _block.SetVector(ColorId, _color.ToVector());
            _block.SetVector(SdfId, new Vector4(IconLibrary.Range, IconLibrary.HalfStroke, half, FxAlpha.Inherited(transform)));
            r.SetPropertyBlock(_block);
            r.localBounds = new Bounds(Vector3.zero, new Vector3(half * 2f, half * 2f, 0.01f));
        }
    }
}
