using UnityEngine;

namespace TenGo.Rendering
{
    /// Cercle néon (bulle, ennemi, anneau) dessiné par le shader SDF
    /// « TenGo/GlowCircle ». Tous les cercles partagent le même quad et le même
    /// matériau ; les réglages propres à chaque cercle passent par un
    /// MaterialPropertyBlock. Les dimensions sont en unités Unity
    /// (1 unité = 100 points SpriteKit, voir CONVENTIONS.md).
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class GlowCircle : MonoBehaviour
    {
        public const string MaterialResource = "Materials/GlowCircle";

        [SerializeField] Color color = new Color(0.369f, 0.922f, 0.831f, 1f); // sarcelle #5EEBD4
        [SerializeField, Min(0f)] float radius = 0.5f;
        [SerializeField, Min(0f)] float thickness = 0.04f;
        [SerializeField, Min(0f)] float glowIntensity = 1f;
        [SerializeField, Min(0f)] float glowRange = 0.3f;
        [SerializeField, Range(0f, 1f)] float fill;
        [SerializeField, Range(0f, 1f)] float coreWhite = 0.35f;
        [SerializeField] int sortingOrder;

        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int RadiusId = Shader.PropertyToID("_Radius");
        static readonly int ThicknessId = Shader.PropertyToID("_Thickness");
        static readonly int GlowIntensityId = Shader.PropertyToID("_GlowIntensity");
        static readonly int GlowRangeId = Shader.PropertyToID("_GlowRange");
        static readonly int FillId = Shader.PropertyToID("_Fill");
        static readonly int CoreWhiteId = Shader.PropertyToID("_CoreWhite");
        static readonly int ExtentId = Shader.PropertyToID("_Extent");

        static Mesh _quad;
        static Material _material;
        MaterialPropertyBlock _block;
        MeshRenderer _renderer;

        public Color Color { get => color; set { color = value; Apply(); } }
        public float Radius { get => radius; set { radius = Mathf.Max(0f, value); Apply(); } }
        public float Thickness { get => thickness; set { thickness = Mathf.Max(0f, value); Apply(); } }
        public float GlowIntensity { get => glowIntensity; set { glowIntensity = Mathf.Max(0f, value); Apply(); } }
        public float GlowRange { get => glowRange; set { glowRange = Mathf.Max(0f, value); Apply(); } }
        public float Fill { get => fill; set { fill = Mathf.Clamp01(value); Apply(); } }
        public float CoreWhite { get => coreWhite; set { coreWhite = Mathf.Clamp01(value); Apply(); } }

        /// Demi-côté du quad : le cercle, son contour et tout son halo.
        public float Extent => radius + thickness * 0.5f + glowRange + 0.01f;

        /// Crée un cercle sous `parent` (ou à la racine).
        public static GlowCircle Create(string name, Transform parent, Vector2 position, Color color, float radius,
            float thickness = 0.04f, float glowIntensity = 1f, float glowRange = 0.3f, float fill = 0f)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer), typeof(GlowCircle));
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            var c = go.GetComponent<GlowCircle>();
            c.color = color;
            c.radius = radius;
            c.thickness = thickness;
            c.glowIntensity = glowIntensity;
            c.glowRange = glowRange;
            c.fill = fill;
            c.Apply();
            return c;
        }

        void OnEnable() => Apply();
        void OnValidate() => Apply();

        /// Pousse les réglages vers le renderer (bloc de propriétés, bornes).
        public void Apply()
        {
            if (_renderer == null) _renderer = GetComponent<MeshRenderer>();
            if (_renderer == null) return;
            var filter = GetComponent<MeshFilter>();
            if (filter.sharedMesh != SharedQuad) filter.sharedMesh = SharedQuad;
            if (_renderer.sharedMaterial == null || _renderer.sharedMaterial.shader != SharedMaterial.shader)
                _renderer.sharedMaterial = SharedMaterial;
            _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
            _renderer.sortingOrder = sortingOrder;

            var extent = Extent;
            _block ??= new MaterialPropertyBlock();
            _renderer.GetPropertyBlock(_block);
            _block.SetColor(ColorId, color);
            _block.SetFloat(RadiusId, radius);
            _block.SetFloat(ThicknessId, thickness);
            _block.SetFloat(GlowIntensityId, glowIntensity);
            _block.SetFloat(GlowRangeId, glowRange);
            _block.SetFloat(FillId, fill);
            _block.SetFloat(CoreWhiteId, coreWhite);
            _block.SetFloat(ExtentId, extent);
            _renderer.SetPropertyBlock(_block);
            // Le vertex shader agrandit le quad à _Extent : bornes à l'avenant.
            _renderer.localBounds = new Bounds(Vector3.zero, new Vector3(extent * 2f, extent * 2f, 0.01f));
        }

        static Material SharedMaterial
        {
            get
            {
                if (_material != null) return _material;
                _material = Resources.Load<Material>(MaterialResource);
                if (_material == null)
                {
                    // Secours (asset absent) : matériau créé à la volée, non sauvegardé.
                    var shader = Shader.Find("TenGo/GlowCircle");
                    _material = new Material(shader) { name = "GlowCircle (runtime)", hideFlags = HideFlags.DontSave };
                }
                return _material;
            }
        }

        /// Quad unité, UV de 0 à 1 ; sa taille réelle vient de _Extent.
        static Mesh SharedQuad
        {
            get
            {
                if (_quad != null) return _quad;
                _quad = new Mesh { name = "GlowCircleQuad", hideFlags = HideFlags.DontSave };
                _quad.SetVertices(new[] { new Vector3(-0.5f, -0.5f), new Vector3(0.5f, -0.5f), new Vector3(0.5f, 0.5f), new Vector3(-0.5f, 0.5f) });
                _quad.SetUVs(0, new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) });
                _quad.SetTriangles(new[] { 0, 2, 1, 0, 3, 2 }, 0);
                _quad.RecalculateBounds();
                return _quad;
            }
        }
    }
}
