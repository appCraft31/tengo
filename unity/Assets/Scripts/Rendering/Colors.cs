using TenGo.Core;
using UnityEngine;

namespace TenGo.Rendering
{
    /// Couleurs en sRGB brut (UIColor), et les dérivations d'UIKit utilisées par
    /// le jeu iOS (`darkened`, `lightened`, `mixedWithWhite`, `ledgeShade`,
    /// `flattened`, `perceivedLuminance`, `readableInk`). Le projet est en
    /// espace Gamma : ces valeurs vont telles quelles aux shaders.
    public static class Colors
    {
        public static Color FromRgb(RgbColor c, float alpha = 1f) => new Color((float)c.R, (float)c.G, (float)c.B, alpha);

        public static Color White(float white, float alpha = 1f) => new Color(white, white, white, alpha);

        public static Color WithAlpha(this Color c, float a) => new Color(c.r, c.g, c.b, a);

        /// Vecteur à passer au shader (alpha multiplié par l'alpha hérité).
        public static Vector4 ToVector(this Color c, float alphaMul = 1f) => new Vector4(c.r, c.g, c.b, c.a * alphaMul);

        /// `darkened(by:)` de BubbleNode : chaque composante moins `f`, bornée à 0.
        public static Color Darkened(this Color c, float f) =>
            new Color(Mathf.Max(0, c.r - f), Mathf.Max(0, c.g - f), Mathf.Max(0, c.b - f), c.a);

        /// `lightened(by:)` : chaque composante plus `f`, bornée à 1.
        public static Color Lightened(this Color c, float f) =>
            new Color(Mathf.Min(1, c.r + f), Mathf.Min(1, c.g + f), Mathf.Min(1, c.b + f), c.a);

        /// `mixedWithWhite(_:)` (0 = inchangée, 1 = blanc).
        public static Color MixedWithWhite(this Color c, float amount) =>
            new Color(c.r + (1 - c.r) * amount, c.g + (1 - c.g) * amount, c.b + (1 - c.b) * amount, c.a);

        /// Luminance perçue (0 = noir, 1 = blanc).
        public static float PerceivedLuminance(this Color c) => 0.299f * c.r + 0.587f * c.g + 0.114f * c.b;

        /// Encre lisible posée sur cette couleur : gris 0,18 sur fond clair, blanc sur fond sombre.
        public static Color ReadableInk(this Color c, float threshold = 0.6f) =>
            c.PerceivedLuminance() > threshold ? White(0.18f) : Color.white;

        /// Teinte du socle (Relief.swift) : même teinte, plus saturée et plus sombre.
        /// Les neutres (blanc, crème) restent neutres : seule la valeur baisse.
        public static Color LedgeShade(this Color c)
        {
            Color.RGBToHSV(new Color(c.r, c.g, c.b), out var h, out var s, out var v);
            float sat = s < 0.08f ? s + 0.04f : Mathf.Min(1f, s * 1.25f + 0.04f);
            var o = Color.HSVToRGB(h, sat, v * (v > 0.35f ? 0.86f : 0.70f));
            o.a = 1f;
            return o;
        }

        /// Couleur opaque équivalente à celle-ci posée sur `background`.
        public static Color Flattened(this Color c, Color background)
        {
            if (c.a >= 1f || c.a <= 0f) return c;
            float a = c.a;
            return new Color(c.r * a + background.r * (1 - a), c.g * a + background.g * (1 - a), c.b * a + background.b * (1 - a), 1f);
        }
    }
}
