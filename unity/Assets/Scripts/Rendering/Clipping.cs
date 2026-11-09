using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace TenGo.Rendering
{
    /// Découpe rectangulaire des pages défilantes (`SKCropNode` d'iOS) :
    /// formes, icônes et traits lisent un rectangle **monde** dans leur bloc de
    /// propriétés (`_TenGoClip`) ; les textes TextMeshPro passent par le
    /// `_ClipRect` de leur shader (mot-clé `UNITY_UI_CLIP_RECT`), exprimé dans
    /// l'espace local de chaque texte, donc recalculé à chaque défilement.
    public static class Clipping
    {
        /// Rectangle qui ne découpe rien.
        public static readonly Vector4 None = new(-1e6f, -1e6f, 1e6f, 1e6f);

        static readonly int TmpClipId = Shader.PropertyToID("_ClipRect");
        static readonly Dictionary<Material, Material> ClippedMaterials = new();

        /// Rectangle monde d'une zone donnée en points de scène (origine au centre).
        public static Vector4 WorldRect(Rect pt) =>
            new(Units.FromPoints(pt.xMin), Units.FromPoints(pt.yMin), Units.FromPoints(pt.xMax), Units.FromPoints(pt.yMax));

        /// Applique la découpe à tout ce qui est sous `root` ; rend les textes à
        /// mettre à jour au défilement (`UpdateTexts`).
        public static List<TextMeshPro> Apply(Transform root, Vector4 worldRect, List<TextMeshPro> texts = null)
        {
            texts ??= new List<TextMeshPro>();
            texts.Clear();
            foreach (var s in root.GetComponentsInChildren<ShapeView>(true)) s.SetClip(worldRect);
            foreach (var i in root.GetComponentsInChildren<VectorIcon>(true)) i.SetClip(worldRect);
            foreach (var l in root.GetComponentsInChildren<StrokeView>(true)) l.SetClip(worldRect);
            foreach (var t in root.GetComponentsInChildren<TextMeshPro>(true))
            {
                var mat = ClippedMaterial(t.fontSharedMaterial);
                if (mat != null && t.fontSharedMaterial != mat) t.fontSharedMaterial = mat;
                texts.Add(t);
            }
            UpdateTexts(texts, worldRect);
            return texts;
        }

        /// Recalcule le `_ClipRect` local de chaque texte (après un défilement).
        public static void UpdateTexts(List<TextMeshPro> texts, Vector4 worldRect)
        {
            var block = new MaterialPropertyBlock();
            foreach (var t in texts)
            {
                if (t == null) continue;
                var tr = t.transform;
                Vector2 a = tr.InverseTransformPoint(new Vector3(worldRect.x, worldRect.y, 0));
                Vector2 b = tr.InverseTransformPoint(new Vector3(worldRect.z, worldRect.w, 0));
                var local = new Vector4(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
                SetBlock(t.renderer, block, local);
                // Replis CJK : sous-maillages dans le même repère que le texte.
                foreach (var sub in t.GetComponentsInChildren<TMP_SubMesh>(true))
                    if (sub != null && sub.renderer != null) SetBlock(sub.renderer, block, local);
            }
        }

        static void SetBlock(Renderer r, MaterialPropertyBlock block, Vector4 rect)
        {
            if (r == null) return;
            r.GetPropertyBlock(block);
            block.SetVector(TmpClipId, rect);
            r.SetPropertyBlock(block);
        }

        /// Copie du matériau de police avec la découpe activée (partagée).
        static Material ClippedMaterial(Material source)
        {
            if (source == null) return null;
            if (source.IsKeywordEnabled("UNITY_UI_CLIP_RECT")) return source;
            if (ClippedMaterials.TryGetValue(source, out var m) && m != null) return m;
            m = new Material(source) { name = source.name + " (découpe)", hideFlags = HideFlags.DontSave };
            m.EnableKeyword("UNITY_UI_CLIP_RECT");
            ClippedMaterials[source] = m;
            return m;
        }
    }
}
