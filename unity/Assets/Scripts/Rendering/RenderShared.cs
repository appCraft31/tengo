using System.Collections.Generic;
using UnityEngine;

namespace TenGo.Rendering
{
    /// Quad et matériaux partagés par les rendus de tenGO (chargés par Resources).
    public static class RenderShared
    {
        static Mesh _quad;
        static readonly Dictionary<string, Material> _materials = new();

        /// Quad unité, UV de 0 à 1 ; sa taille réelle est fixée dans le vertex shader.
        public static Mesh Quad
        {
            get
            {
                if (_quad != null) return _quad;
                _quad = new Mesh { name = "TenGoQuad", hideFlags = HideFlags.DontSave };
                _quad.SetVertices(new[] { new Vector3(-0.5f, -0.5f), new Vector3(0.5f, -0.5f), new Vector3(0.5f, 0.5f), new Vector3(-0.5f, 0.5f) });
                _quad.SetUVs(0, new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) });
                _quad.SetTriangles(new[] { 0, 2, 1, 0, 3, 2 }, 0);
                _quad.RecalculateBounds();
                return _quad;
            }
        }

        /// Matériau `Resources/Materials/<name>` ou, à défaut, créé à la volée
        /// depuis le shader `TenGo/<name>` (éditeur, tests).
        public static Material Material(string name)
        {
            if (_materials.TryGetValue(name, out var m) && m != null) return m;
            m = Resources.Load<Material>("Materials/" + name);
            if (m == null)
            {
                var s = Shader.Find("TenGo/" + name);
                if (s == null) { Debug.LogError($"[Rendering] Shader introuvable : TenGo/{name}"); return null; }
                m = new Material(s) { name = name + " (runtime)", hideFlags = HideFlags.DontSave };
            }
            _materials[name] = m;
            return m;
        }

        /// Prépare un MeshRenderer (sans ombres ni sondes) avec `mesh` et `material`.
        public static MeshRenderer Setup(GameObject go, Mesh mesh, Material material)
        {
            var filter = go.GetComponent<MeshFilter>();
            if (filter == null) filter = go.AddComponent<MeshFilter>();
            if (filter.sharedMesh != mesh) filter.sharedMesh = mesh;
            var r = go.GetComponent<MeshRenderer>();
            if (r == null) r = go.AddComponent<MeshRenderer>();
            if (r.sharedMaterial != material) r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            return r;
        }

        /// Détruit un objet en jeu comme en éditeur.
        public static void Destroy(Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Object.Destroy(o); else Object.DestroyImmediate(o);
        }

        /// Crée un GameObject vide (`SKNode`) sous `parent`.
        public static Transform Node(string name, Transform parent, Vector2 localPos = default)
        {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            return go.transform;
        }
    }
}
