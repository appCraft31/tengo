using System.Collections.Generic;
using TenGo.Fx;
using UnityEngine;

namespace TenGo.Rendering
{
    /// Trait d'un `CGPath` (polyligne) : `SKShapeNode(path:)` avec `lineCap` et
    /// `lineJoin` ronds, pointillés facultatifs. Sert au tracé (TrailRenderer),
    /// à la pousse de SeedArt et aux aiguilles de l'horloge. 32 points au plus ;
    /// points en unités locales, épaisseur et tirets en points.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class StrokeView : MonoBehaviour, IFxAlphaTarget
    {
        public const int MaxPoints = 32;
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int LineId = Shader.PropertyToID("_Line");
        static readonly int ParamsId = Shader.PropertyToID("_Params");
        static readonly int PointsId = Shader.PropertyToID("_Points");

        readonly List<Vector2> _points = new();
        readonly Vector4[] _packed = new Vector4[MaxPoints];
        Color _color = Color.white;
        float _widthPt = 6f;
        float _dashOnPt, _dashOffPt;
        int _order;
        Mesh _mesh;
        MeshRenderer _renderer;
        MaterialPropertyBlock _block;

        public IReadOnlyList<Vector2> Points => _points;
        public Color Color { get => _color; set { _color = value; Apply(); } }
        public float WidthPt { get => _widthPt; set { _widthPt = value; Apply(); } }
        public int SortingOrder { get => _order; set { _order = value; Apply(); } }

        public static StrokeView Create(string name, Transform parent, IEnumerable<Vector2> points, Color color, float widthPt,
            int sortingOrder, float dashOnPt = 0f, float dashOffPt = 0f)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer), typeof(StrokeView));
            if (parent != null) go.transform.SetParent(parent, false);
            var s = go.GetComponent<StrokeView>();
            s._color = color;
            s._widthPt = widthPt;
            s._order = sortingOrder;
            s._dashOnPt = dashOnPt;
            s._dashOffPt = dashOffPt;
            s.SetPoints(points);
            return s;
        }

        /// Remplace la polyligne (au-delà de 32 points, les derniers sont ignorés).
        public void SetPoints(IEnumerable<Vector2> points)
        {
            _points.Clear();
            foreach (var p in points)
            {
                if (_points.Count >= MaxPoints) break;
                _points.Add(p);
            }
            Apply();
        }

        /// `copy(dashingWithPhase: 0, lengths: [on, off])`, en points ; 0 = trait plein.
        public void SetDash(float onPt, float offPt) { _dashOnPt = onPt; _dashOffPt = offPt; Apply(); }

        public void OnInheritedAlphaChanged() => Apply();

        public void Apply()
        {
            var mat = RenderShared.Material("Trail");
            if (mat == null) return;
            if (_mesh == null) _mesh = new Mesh { name = "Stroke", hideFlags = HideFlags.DontSave };
            _renderer = RenderShared.Setup(gameObject, _mesh, mat);
            _renderer.sortingOrder = _order;
            float halfW = Units.FromPoints(_widthPt * 0.5f);
            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);
            float cum = 0f;
            for (int i = 0; i < MaxPoints; i++)
            {
                if (i < _points.Count)
                {
                    if (i > 0) cum += Vector2.Distance(_points[i - 1], _points[i]);
                    _packed[i] = new Vector4(_points[i].x, _points[i].y, cum, 0);
                    min = Vector2.Min(min, _points[i]);
                    max = Vector2.Max(max, _points[i]);
                }
                else _packed[i] = Vector4.zero;
            }
            if (_points.Count == 0) { min = max = Vector2.zero; }
            float pad = halfW + 0.02f;
            min -= new Vector2(pad, pad);
            max += new Vector2(pad, pad);
            _mesh.Clear();
            _mesh.SetVertices(new[] { new Vector3(min.x, min.y), new Vector3(max.x, min.y), new Vector3(max.x, max.y), new Vector3(min.x, max.y) });
            _mesh.SetTriangles(new[] { 0, 2, 1, 0, 3, 2 }, 0);
            _mesh.RecalculateBounds();
            _block ??= new MaterialPropertyBlock();
            _block.SetVector(ColorId, _color.ToVector());
            float period = _dashOnPt + _dashOffPt;
            _block.SetVector(LineId, new Vector4(halfW, _points.Count, Units.FromPoints(period), Units.FromPoints(_dashOnPt)));
            _block.SetVector(ParamsId, new Vector4(FxAlpha.Inherited(transform), 0, 0, 0));
            _block.SetVectorArray(PointsId, _packed);
            _renderer.SetPropertyBlock(_block);
        }

        void OnDestroy() => RenderShared.Destroy(_mesh);
    }
}
