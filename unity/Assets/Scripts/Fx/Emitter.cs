using System.Collections.Generic;
using TenGo.Rendering;
using UnityEngine;

namespace TenGo.Fx
{
    /// Émetteur de particules simulé sur le CPU (`SKEmitterNode`), avancé par
    /// `FxTime` (reproductible dans les captures) : un maillage de disques doux,
    /// shader TenGo/Dots. Sert aux confettis de la victoire.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class Emitter : MonoBehaviour, IFxTickable
    {
        struct P
        {
            public Vector2 Pos, Vel;
            public float Age, Life, Alpha, Scale;
            public Color Color;
        }

        static readonly int ParamsId = Shader.PropertyToID("_Params");
        readonly List<P> _ps = new();
        readonly List<Vector3> _verts = new();
        readonly List<Vector2> _uvs = new();
        readonly List<Color> _colors = new();
        readonly List<int> _tris = new();
        EmitterSpec _spec;
        System.Random _rng;
        Mesh _mesh;
        MaterialPropertyBlock _block;
        float _time, _birthAcc;
        int _emitted;

        public int AliveCount => _ps.Count;
        public int EmittedCount => _emitted;

        public static Emitter Create(Transform parent, Vector2 localPos, EmitterSpec spec, int sortingOrder, System.Random rng)
        {
            var go = new GameObject("Emitter", typeof(MeshFilter), typeof(MeshRenderer), typeof(Emitter));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var e = go.GetComponent<Emitter>();
            e._spec = spec;
            e._rng = rng ?? new System.Random();
            e._mesh = new Mesh { name = "Particles", hideFlags = HideFlags.DontSave };
            e._mesh.MarkDynamic();
            var r = RenderShared.Setup(go, e._mesh, RenderShared.Material("Dots"));
            r.sortingOrder = sortingOrder;
            e._block = new MaterialPropertyBlock();
            FxTime.Register(e);
            return e;
        }

        float Range(float v, float range) => v + ((float)_rng.NextDouble() - 0.5f) * range;

        void Emit()
        {
            float angle = Range(_spec.EmissionAngle, _spec.EmissionAngleRange);
            float speed = Range(_spec.Speed, _spec.SpeedRange);
            float blend = Mathf.Clamp01(Range(_spec.ColorBlendFactor, _spec.ColorBlendFactorRange));
            _ps.Add(new P
            {
                Pos = new Vector2(Range(0f, _spec.PositionRangePt.x), Range(0f, _spec.PositionRangePt.y)),
                Vel = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed,
                Life = Mathf.Max(0.01f, Range(_spec.Lifetime, _spec.LifetimeRange)),
                Alpha = Mathf.Clamp01(Range(_spec.Alpha, _spec.AlphaRange)),
                Scale = Mathf.Max(0f, Range(_spec.Scale, _spec.ScaleRange)),
                // Texture blanche teintée : blanc → couleur selon colorBlendFactor.
                Color = Color.Lerp(Color.white, _spec.Color, blend),
            });
            _emitted++;
        }

        void Simulate(float dt)
        {
            _time += dt;
            if (_spec.NumToEmit <= 0 || _emitted < _spec.NumToEmit)
            {
                _birthAcc += _spec.BirthRate * dt;
                while (_birthAcc >= 1f && (_spec.NumToEmit <= 0 || _emitted < _spec.NumToEmit))
                {
                    _birthAcc -= 1f;
                    Emit();
                }
            }
            for (int i = _ps.Count - 1; i >= 0; i--)
            {
                var p = _ps[i];
                p.Age += dt;
                if (p.Age >= p.Life) { _ps.RemoveAt(i); continue; }
                p.Vel.y += _spec.YAcceleration * dt;
                p.Pos += p.Vel * dt;
                p.Alpha = Mathf.Clamp01(p.Alpha + _spec.AlphaSpeed * dt);
                p.Scale = Mathf.Max(0f, p.Scale + _spec.ScaleSpeed * dt);
                _ps[i] = p;
            }
        }

        public bool FxTick(float dt)
        {
            if (this == null) return false;
            Simulate(dt);
            if (_spec.RemoveAfter >= 0f && _time >= _spec.RemoveAfter)
            {
                RenderShared.Destroy(gameObject);
                return false;
            }
            Rebuild();
            return true;
        }

        void Rebuild()
        {
            _verts.Clear(); _uvs.Clear(); _colors.Clear(); _tris.Clear();
            foreach (var p in _ps)
            {
                float half = Units.FromPoints(_spec.BaseSizePt * p.Scale) * 0.5f;
                if (half <= 0f || p.Alpha <= 0f) continue;
                var c = Units.FromPoints(1f) * (Vector3)p.Pos;
                int b = _verts.Count;
                _verts.Add(c + new Vector3(-half, -half)); _verts.Add(c + new Vector3(half, -half));
                _verts.Add(c + new Vector3(half, half)); _verts.Add(c + new Vector3(-half, half));
                _uvs.Add(new Vector2(0, 0)); _uvs.Add(new Vector2(1, 0)); _uvs.Add(new Vector2(1, 1)); _uvs.Add(new Vector2(0, 1));
                var col = new Color(p.Color.r, p.Color.g, p.Color.b, p.Alpha);
                _colors.Add(col); _colors.Add(col); _colors.Add(col); _colors.Add(col);
                _tris.Add(b); _tris.Add(b + 2); _tris.Add(b + 1); _tris.Add(b); _tris.Add(b + 3); _tris.Add(b + 2);
            }
            _mesh.Clear();
            _mesh.SetVertices(_verts);
            _mesh.SetUVs(0, _uvs);
            _mesh.SetColors(_colors);
            _mesh.SetTriangles(_tris, 0);
            _mesh.RecalculateBounds();
            var bounds = _mesh.bounds;
            bounds.Expand(0.5f);
            _mesh.bounds = bounds;
            _block.SetVector(ParamsId, new Vector4(_spec.Additive ? 1 : 0, FxAlpha.Inherited(transform), 0, 0));
            GetComponent<MeshRenderer>().SetPropertyBlock(_block);
        }

        void OnDestroy()
        {
            FxTime.Unregister(this);
            RenderShared.Destroy(_mesh);
        }
    }
}
