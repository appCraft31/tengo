using System.Collections.Generic;
using TenGo.Rendering;
using UnityEngine;

namespace TenGo.Fx
{
    /// Éclats radiaux de PopEffects.addSparks : chaque éclat est un disque doux
    /// (FXTextures.dot) qui file vers sa cible (easeOut), retombe de 8 pt
    /// (easeIn) et s'efface linéairement. Tous les éclats d'une bulle partagent
    /// un maillage (un seul appel de rendu, comme le lot SpriteKit), avancé par `FxTime`.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class SparkCloud : MonoBehaviour, IFxTickable
    {
        struct Spark
        {
            public Vector2 Target;   // points
            public float Size, Duration, Alpha;
            public Color Color;
        }

        static readonly int ParamsId = Shader.PropertyToID("_Params");
        readonly List<Spark> _sparks = new();
        readonly List<Vector3> _verts = new();
        readonly List<Vector2> _uvs = new();
        readonly List<Color> _colors = new();
        readonly List<int> _tris = new();
        Mesh _mesh;
        MaterialPropertyBlock _block;
        float _time;

        public int Count => _sparks.Count;
        public float Elapsed => _time;

        /// `count` éclats de `color` (mêlé de 0 à 35 % de blanc), portée `reach` en points.
        public static SparkCloud Create(Transform parent, Color color, int count, (float min, float max) reach,
            int sortingOrder, System.Random rng)
        {
            var go = new GameObject("Sparks", typeof(MeshFilter), typeof(MeshRenderer), typeof(SparkCloud));
            go.transform.SetParent(parent, false);
            var c = go.GetComponent<SparkCloud>();
            for (int i = 0; i < count; i++)
            {
                float size = Range(rng, 6, 14);
                float angle = Range(rng, 0, Mathf.PI * 2f);
                float dist = Range(rng, reach.min, reach.max);
                c._sparks.Add(new Spark
                {
                    Size = size,
                    Color = color.MixedWithWhite(Range(rng, 0, 0.35f)),
                    Alpha = 0.9f,
                    Target = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * dist,
                    Duration = Range(rng, 0.30f, 0.45f),
                });
            }
            c._mesh = new Mesh { name = "Sparks", hideFlags = HideFlags.DontSave };
            c._mesh.MarkDynamic();
            var r = RenderShared.Setup(go, c._mesh, RenderShared.Material("Dots"));
            r.sortingOrder = sortingOrder;
            c._block = new MaterialPropertyBlock();
            c.Rebuild();
            FxTime.Register(c);
            return c;
        }

        static float Range(System.Random rng, float a, float b) => a + (float)rng.NextDouble() * (b - a);

        public bool FxTick(float dt)
        {
            if (this == null) return false;
            _time += dt;
            Rebuild();
            return true;
        }

        void Rebuild()
        {
            _verts.Clear(); _uvs.Clear(); _colors.Clear(); _tris.Clear();
            foreach (var s in _sparks)
            {
                float p = Mathf.Clamp01(_time / s.Duration);
                if (p >= 1f) continue;
                var pos = s.Target * TwEasing.Evaluate(TwEase.EaseOut, p) + new Vector2(0, -8f * TwEasing.Evaluate(TwEase.EaseIn, p));
                float a = s.Alpha * (1f - p);
                var c = Units.FromPoints(1f) * (Vector3)pos;
                float h = Units.FromPoints(s.Size * 0.5f);
                int b = _verts.Count;
                _verts.Add(c + new Vector3(-h, -h)); _verts.Add(c + new Vector3(h, -h));
                _verts.Add(c + new Vector3(h, h)); _verts.Add(c + new Vector3(-h, h));
                _uvs.Add(new Vector2(0, 0)); _uvs.Add(new Vector2(1, 0)); _uvs.Add(new Vector2(1, 1)); _uvs.Add(new Vector2(0, 1));
                var col = new Color(s.Color.r, s.Color.g, s.Color.b, a);
                _colors.Add(col); _colors.Add(col); _colors.Add(col); _colors.Add(col);
                _tris.Add(b); _tris.Add(b + 2); _tris.Add(b + 1); _tris.Add(b); _tris.Add(b + 3); _tris.Add(b + 2);
            }
            _mesh.Clear();
            _mesh.SetVertices(_verts);
            _mesh.SetUVs(0, _uvs);
            _mesh.SetColors(_colors);
            _mesh.SetTriangles(_tris, 0);
            _mesh.RecalculateBounds();
            _block.SetVector(ParamsId, new Vector4(0, FxAlpha.Inherited(transform), 0, 0));
            GetComponent<MeshRenderer>().SetPropertyBlock(_block);
        }

        void OnDestroy()
        {
            FxTime.Unregister(this);
            RenderShared.Destroy(_mesh);
        }
    }
}
