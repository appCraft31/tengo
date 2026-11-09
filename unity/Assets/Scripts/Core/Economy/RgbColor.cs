#nullable enable
namespace TenGo.Core
{
    /// Couleur RVB opaque en composantes 0–1 (`UIColor(red:green:blue:alpha: 1)`).
    /// Core ne connaît pas `UnityEngine.Color` : la couche rendu convertit.
    public readonly struct RgbColor
    {
        public RgbColor(double r, double g, double b)
        {
            R = r;
            G = g;
            B = b;
        }

        public double R { get; }
        public double G { get; }
        public double B { get; }

        /// Gris neutre (`.gray` d'UIKit, 0,5).
        public static readonly RgbColor Gray = new RgbColor(0.5, 0.5, 0.5);

        /// Luminance perçue (0 = noir, 1 = blanc).
        public double PerceivedLuminance => 0.299 * R + 0.587 * G + 0.114 * B;

        /// Encre lisible posée sur cette couleur : foncée (gris 0,18) sur fond
        /// clair, blanche sur fond sombre.
        public RgbColor ReadableInk(double threshold = 0.6) =>
            PerceivedLuminance > threshold ? new RgbColor(0.18, 0.18, 0.18) : new RgbColor(1, 1, 1);
    }
}
