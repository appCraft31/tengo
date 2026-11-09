using TenGo.Fx;
using UnityEngine;

namespace TenGo.Rendering
{
    /// Type de forme du shader `TenGo/Shape`.
    public enum ShapeKind
    {
        /// Rectangle arrondi ; un cercle est un carré d'angle égal au rayon.
        RoundRect = 0,
        Ellipse = 1,
        /// Disque doux de FXTextures.dot (dégradé radial), teinté par `Fill`.
        SoftDot = 2,
        /// Anneau de FXTextures.ring (trait de 6/128 du diamètre), teinté par `Fill`.
        Ring = 3,
    }

    /// `SKShapeNode` (et les sprites de FXTextures) : forme SDF nette à toute
    /// densité, remplissage + contour centré (`lineWidth`), alpha hérité
    /// (`FxAlpha`). Tailles en **points**, couleurs sRGB (`Colors`).
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class ShapeView : MonoBehaviour, IFxAlphaTarget
    {
        static readonly int FillId = Shader.PropertyToID("_FillColor");
        static readonly int StrokeId = Shader.PropertyToID("_StrokeColor");
        static readonly int BoxId = Shader.PropertyToID("_Box");
        static readonly int ParamsId = Shader.PropertyToID("_Params");
        static readonly int ExtentId = Shader.PropertyToID("_Extent");

        [SerializeField] ShapeKind kind;
        [SerializeField] Vector2 sizePt = new(78, 78);
        [SerializeField] float cornerPt = 39;
        [SerializeField] Color fill = Color.white;
        [SerializeField] Color stroke = Color.clear;
        [SerializeField] float lineWidthPt;
        [SerializeField] float softPt;
        [SerializeField] bool additive;
        [SerializeField] int sortingOrder;
        MaterialPropertyBlock _block;
        MeshRenderer _renderer;

        public ShapeKind Kind { get => kind; set { kind = value; Apply(); } }
        /// Taille totale (largeur, hauteur) en points.
        public Vector2 SizePt { get => sizePt; set { sizePt = value; Apply(); } }
        public float CornerPt { get => cornerPt; set { cornerPt = value; Apply(); } }
        public Color Fill { get => fill; set { fill = value; Apply(); } }
        public Color Stroke { get => stroke; set { stroke = value; Apply(); } }
        public float LineWidthPt { get => lineWidthPt; set { lineWidthPt = value; Apply(); } }
        /// Flou du bord en points (ombre douce) ; 0 = bord net d'un pixel.
        public float SoftPt { get => softPt; set { softPt = value; Apply(); } }
        /// `blendMode = .add`.
        public bool Additive { get => additive; set { additive = value; Apply(); } }
        public int SortingOrder { get => sortingOrder; set { sortingOrder = value; Apply(); } }

        public static ShapeView Create(string name, Transform parent, Vector2 localPos, ShapeKind kind, Vector2 sizePt,
            Color fill, Color stroke = default, float lineWidthPt = 0f, float cornerPt = 0f, int sortingOrder = 0)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer), typeof(ShapeView));
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var s = go.GetComponent<ShapeView>();
            s.kind = kind; s.sizePt = sizePt; s.fill = fill; s.stroke = stroke; s.lineWidthPt = lineWidthPt;
            s.cornerPt = cornerPt; s.sortingOrder = sortingOrder;
            s.Apply();
            return s;
        }

        /// `SKShapeNode(circleOfRadius:)`.
        public static ShapeView Circle(string name, Transform parent, Vector2 localPos, float radiusPt, Color fill,
            Color stroke = default, float lineWidthPt = 0f, int sortingOrder = 0) =>
            Create(name, parent, localPos, ShapeKind.RoundRect, new Vector2(radiusPt * 2, radiusPt * 2), fill, stroke, lineWidthPt, radiusPt, sortingOrder);

        /// `SKShapeNode(ellipseOf:)`.
        public static ShapeView Ellipse(string name, Transform parent, Vector2 localPos, Vector2 sizePt, Color fill,
            Color stroke = default, float lineWidthPt = 0f, int sortingOrder = 0) =>
            Create(name, parent, localPos, ShapeKind.Ellipse, sizePt, fill, stroke, lineWidthPt, 0f, sortingOrder);

        /// `SKShapeNode(rectOf:cornerRadius:)`.
        public static ShapeView Rect(string name, Transform parent, Vector2 localPos, Vector2 sizePt, float cornerPt, Color fill,
            Color stroke = default, float lineWidthPt = 0f, int sortingOrder = 0) =>
            Create(name, parent, localPos, ShapeKind.RoundRect, sizePt, fill, stroke, lineWidthPt, cornerPt, sortingOrder);

        /// Sprite `FXTextures.dot` de `sizePt` de diamètre, teinté (`colorBlendFactor = 1`).
        public static ShapeView Dot(string name, Transform parent, Vector2 localPos, float sizePt, Color color,
            bool additive, int sortingOrder = 0)
        {
            var s = Create(name, parent, localPos, ShapeKind.SoftDot, new Vector2(sizePt, sizePt), color, default, 0f, 0f, sortingOrder);
            s.Additive = additive;
            return s;
        }

        /// Sprite `FXTextures.ring` de `sizePt` de diamètre, teinté.
        public static ShapeView Ring(string name, Transform parent, Vector2 localPos, float sizePt, Color color, int sortingOrder = 0) =>
            Create(name, parent, localPos, ShapeKind.Ring, new Vector2(sizePt, sizePt), color, default, 0f, 0f, sortingOrder);

        void OnEnable() => Apply();
        void OnValidate() => Apply();
        public void OnInheritedAlphaChanged() => Apply();

        public void Apply()
        {
            if (_renderer == null) _renderer = GetComponent<MeshRenderer>();
            if (_renderer == null) return;
            var mat = RenderShared.Material("Shape");
            if (mat == null) return;
            RenderShared.Setup(gameObject, RenderShared.Quad, mat);
            _renderer.sortingOrder = sortingOrder;
            float hx = Units.FromPoints(sizePt.x * 0.5f), hy = Units.FromPoints(sizePt.y * 0.5f);
            float lw = Units.FromPoints(lineWidthPt);
            float soft = Units.FromPoints(softPt);
            float pad = lw * 0.5f + soft * 1.5f + 0.02f;
            float ex = hx + pad, ey = hy + pad;
            _block ??= new MaterialPropertyBlock();
            _block.SetVector(FillId, fill.ToVector());
            _block.SetVector(StrokeId, stroke.ToVector());
            _block.SetVector(BoxId, new Vector4(hx, hy, Units.FromPoints(cornerPt), lw));
            _block.SetVector(ParamsId, new Vector4((int)kind, soft, additive ? 1f : 0f, FxAlpha.Inherited(transform)));
            _block.SetVector(ExtentId, new Vector4(ex, ey, 0, 0));
            _renderer.SetPropertyBlock(_block);
            _renderer.localBounds = new Bounds(Vector3.zero, new Vector3(ex * 2f, ey * 2f, 0.01f));
        }
    }
}
