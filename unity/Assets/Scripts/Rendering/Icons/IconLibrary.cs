using System.Collections.Generic;
using UnityEngine;

namespace TenGo.Rendering
{
    /// Champs de distance des icônes vectorielles, calculés à l'exécution
    /// depuis les tracés SVG (aucune image dans le projet) et mis en cache.
    ///
    /// Choix : les 13 icônes ne sont que des traits de 2 unités à bouts ronds.
    /// La distance exacte aux segments aplatis, stockée dans un champ 128 × 128,
    /// donne un rendu net jusqu'à ~4× sa résolution (≈ 500 px), se teinte
    /// librement et suit l'alpha hérité sans artefact aux jonctions, ce que ni
    /// un maillage de traits (bords crénelés sans MSAA, recouvrements visibles
    /// en translucide) ni une image rastérisée à taille fixe ne permettent.
    /// Coût : ~10 ms par icône au premier affichage.
    public static partial class IconLibrary
    {
        /// Côté du `viewBox` des SVG.
        public const float ViewBox = 24f;
        /// Demi-épaisseur du trait (stroke-width 2).
        public const float HalfStroke = 1f;
        /// Marge autour du `viewBox` couverte par le champ (unités SVG).
        public const float Pad = 2f;
        /// Portée de la distance encodée (± unités SVG).
        public const float Range = 3f;
        public const int Resolution = 128;

        static readonly Dictionary<VectorIconName, Texture2D> _cache = new();
        static readonly Dictionary<VectorIconName, List<List<Vector2>>> _paths = new();

        /// Polylignes de l'icône (repère SVG, y vers le bas).
        public static List<List<Vector2>> Paths(VectorIconName name)
        {
            if (_paths.TryGetValue(name, out var p)) return p;
            p = SvgPath.ParseElements(SvgOf(name));
            _paths[name] = p;
            return p;
        }

        /// Source SVG de l'icône (catalogue iOS ou famille au trait de la refonte).
        public static string SvgOf(VectorIconName name)
        {
            var key = name.ToString();
            return Svg.TryGetValue(key, out var s) ? s : LineSvg[key];
        }

        /// Famille au trait de la refonte (trait 2,1 au lieu de 2).
        public static bool IsLine(VectorIconName name) => LineSvg.ContainsKey(name.ToString());

        /// Demi-épaisseur du trait de l'icône, en unités SVG.
        public static float HalfStrokeOf(VectorIconName name) => IsLine(name) ? 1.05f : HalfStroke;

        /// Distance du point `q` (unités SVG) au trait central de l'icône.
        public static float Distance(List<List<Vector2>> paths, Vector2 q)
        {
            float best = float.MaxValue;
            foreach (var pl in paths)
            {
                if (pl.Count == 1) best = Mathf.Min(best, (q - pl[0]).sqrMagnitude);
                for (int i = 0; i + 1 < pl.Count; i++)
                {
                    var a = pl[i];
                    var ab = pl[i + 1] - a;
                    float t = Mathf.Clamp01(Vector2.Dot(q - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-12f));
                    best = Mathf.Min(best, (q - (a + ab * t)).sqrMagnitude);
                }
            }
            return Mathf.Sqrt(best);
        }

        /// Champ de distance de l'icône (R8, bilinéaire). Texel (0,0) en bas à gauche.
        public static Texture2D Field(VectorIconName name)
        {
            if (_cache.TryGetValue(name, out var tex) && tex != null) return tex;
            var paths = Paths(name);
            const int n = Resolution;
            var data = new byte[n * n];
            float span = ViewBox + Pad * 2f;
            for (int y = 0; y < n; y++)
            {
                // y du SVG vers le bas : la rangée du haut de la texture = y SVG minimal.
                float sy = -Pad + (n - 1 - y + 0.5f) / n * span;
                for (int x = 0; x < n; x++)
                {
                    float sx = -Pad + (x + 0.5f) / n * span;
                    float d = Distance(paths, new Vector2(sx, sy));
                    data[y * n + x] = (byte)Mathf.RoundToInt(Mathf.Clamp01(d / Range * 0.5f + 0.5f) * 255f);
                }
            }
            tex = new Texture2D(n, n, TextureFormat.R8, false, true)
            {
                name = "Icon_" + name, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave,
            };
            tex.LoadRawTextureData(data);
            tex.Apply(false, true);
            _cache[name] = tex;
            return tex;
        }
    }
}
