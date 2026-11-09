using TenGo.Fx;
using UnityEngine;

namespace TenGo.Rendering
{
    /// Trait sinusoïdal du thème Océan (`ThemeBackground.makeSineNode`), centré
    /// sur l'origine du nœud : largeur, amplitude, longueur d'onde et épaisseur en points.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class WaveView : MonoBehaviour, IFxAlphaTarget
    {
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int WaveId = Shader.PropertyToID("_Wave");
        static readonly int ParamsId = Shader.PropertyToID("_Params");

        Color _color;
        Vector4 _wave;
        int _order;
        MaterialPropertyBlock _block;

        public static WaveView Create(Transform parent, Vector2 localPos, float widthPt, float amplitudePt, float wavelengthPt,
            float lineWidthPt, Color color, int sortingOrder)
        {
            var go = new GameObject("Wave", typeof(MeshFilter), typeof(MeshRenderer), typeof(WaveView));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var w = go.GetComponent<WaveView>();
            w._color = color;
            w._wave = new Vector4(Units.FromPoints(amplitudePt), Units.FromPoints(wavelengthPt), Units.FromPoints(lineWidthPt * 0.5f), Units.FromPoints(widthPt * 0.5f));
            w._order = sortingOrder;
            w.Apply();
            return w;
        }

        public void OnInheritedAlphaChanged() => Apply();

        void Apply()
        {
            var mat = RenderShared.Material("Wave");
            if (mat == null) return;
            var r = RenderShared.Setup(gameObject, RenderShared.Quad, mat);
            r.sortingOrder = _order;
            _block ??= new MaterialPropertyBlock();
            _block.SetVector(ColorId, _color.ToVector());
            _block.SetVector(WaveId, _wave);
            _block.SetVector(ParamsId, new Vector4(FxAlpha.Inherited(transform), 0, 0, 0));
            r.SetPropertyBlock(_block);
            r.localBounds = new Bounds(Vector3.zero, new Vector3((_wave.w + _wave.z) * 2f, (_wave.x + _wave.z + 0.02f) * 2f, 0.01f));
        }
    }
}
