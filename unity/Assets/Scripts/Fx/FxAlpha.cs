using System.Collections.Generic;
using UnityEngine;

namespace TenGo.Fx
{
    /// Alpha d'un nœud, multiplié dans toute sa descendance comme `SKNode.alpha`.
    /// Les rendus (`GlowCircle`, `GlowSprite`, `FxText`…) lisent `Inherited`.
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class FxAlpha : MonoBehaviour
    {
        [SerializeField, Range(0f, 1f)] float alpha = 1f;
        static readonly List<IFxAlphaTarget> _buffer = new();

        public float Alpha
        {
            get => alpha;
            set
            {
                value = Mathf.Clamp01(value);
                if (Mathf.Approximately(value, alpha)) return;
                alpha = value;
                Propagate();
            }
        }

        void OnValidate() => Propagate();
        void OnTransformParentChanged() => Propagate();

        /// Prévient les rendus de la descendance.
        public void Propagate()
        {
            _buffer.Clear();
            GetComponentsInChildren(true, _buffer);
            foreach (var t in _buffer) t.OnInheritedAlphaChanged();
            _buffer.Clear();
        }

        /// Produit des alphas de `t` et de ses ancêtres.
        public static float Inherited(Transform t)
        {
            float a = 1f;
            for (var p = t; p != null; p = p.parent)
                if (p.TryGetComponent(out FxAlpha fa)) a *= fa.alpha;
            return a;
        }

        /// Le composant de `go` (créé si absent).
        public static FxAlpha Of(GameObject go) => go.TryGetComponent(out FxAlpha a) ? a : go.AddComponent<FxAlpha>();
    }
}
