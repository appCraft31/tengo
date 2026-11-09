#nullable enable
using System.Collections.Generic;
using System.Linq;

namespace TenGo.Core
{
    /// Catalogue des thèmes, repris de ThemeManager.swift (palettes
    /// harmonisées et contrôlées en contraste), sauf le thème par défaut, refait pour la 4.0. Le défaut en premier, puis
    /// l'ordre d'affichage de la boutique.
    public static class ThemeCatalog
    {
        private static RgbColor C(double r, double g, double b) => new RgbColor(r, g, b);

        /// Couleur `#RRGGBB`.
        private static RgbColor H(int rgb) => new RgbColor(((rgb >> 16) & 0xFF) / 255.0, ((rgb >> 8) & 0xFF) / 255.0, (rgb & 0xFF) / 255.0);

        public static readonly IReadOnlyList<Theme> All = new[]
        {
            // Refonte 4.0 (direction A « plat crème », design/refonte/systeme.css) :
            // fond crème, encre #34312C, accent #65BD90 et palette des bulles
            // calculée pour rester distincte en deutéranopie, protanopie et
            // tritanopie. Les autres thèmes seront revus avec la Boutique.
            new Theme("default", 0,
                background: H(0xF7F3EC),
                accent: H(0x65BD90),
                digit: H(0x34312C),
                logo: H(0x34312C),
                bubbles: new[] { H(0xFACFCC), H(0xDB9661), H(0xD7C06C), H(0xC6E99B), H(0x65BD90), H(0x7FEFFB), H(0x97C6F7), H(0xA99BE6), H(0xF0A5D7) }),
            new Theme("forest", 150,
                background: C(0.93, 0.95, 0.88),
                accent: C(0.55, 0.68, 0.48),
                digit: C(0.20, 0.16, 0.12),
                logo: C(0.22, 0.32, 0.22),
                bubbles: new[] { C(0.70, 0.80, 0.60), C(0.62, 0.74, 0.55), C(0.80, 0.82, 0.62), C(0.74, 0.68, 0.50), C(0.66, 0.72, 0.60), C(0.82, 0.72, 0.56), C(0.58, 0.70, 0.58), C(0.76, 0.78, 0.66), C(0.68, 0.66, 0.52) }),
            new Theme("ocean", 200,
                background: C(0.91, 0.96, 0.97),
                accent: C(0.36, 0.72, 0.78),
                digit: C(0.08, 0.22, 0.33),
                logo: C(0.10, 0.28, 0.40),
                bubbles: new[] { C(0.62, 0.85, 0.86), C(0.46, 0.78, 0.80), C(0.40, 0.74, 0.72), C(0.52, 0.80, 0.66), C(0.66, 0.84, 0.72), C(0.45, 0.71, 0.83), C(0.38, 0.62, 0.80), C(0.50, 0.68, 0.86), C(0.63, 0.78, 0.88) }),
            new Theme("desert", 200,
                background: C(0.96, 0.91, 0.83),
                accent: C(0.85, 0.55, 0.40),
                digit: C(0.30, 0.18, 0.12),
                logo: C(0.42, 0.26, 0.18),
                bubbles: new[] { C(0.94, 0.84, 0.68), C(0.93, 0.78, 0.62), C(0.91, 0.72, 0.55), C(0.90, 0.68, 0.58), C(0.89, 0.62, 0.50), C(0.87, 0.58, 0.45), C(0.85, 0.66, 0.62), C(0.82, 0.55, 0.48), C(0.80, 0.50, 0.42) }),
            new Theme("candy", 250,
                background: C(0.98, 0.94, 0.95),
                accent: C(0.96, 0.55, 0.72),
                digit: C(0.28, 0.16, 0.30),
                logo: C(0.30, 0.18, 0.33),
                bubbles: new[] { C(0.97, 0.62, 0.74), C(0.98, 0.66, 0.62), C(0.99, 0.79, 0.63), C(0.98, 0.90, 0.58), C(0.80, 0.91, 0.62), C(0.62, 0.89, 0.74), C(0.64, 0.85, 0.93), C(0.74, 0.74, 0.93), C(0.86, 0.68, 0.91) }),
            new Theme("space", 300,
                background: C(0.09, 0.08, 0.18),
                accent: C(0.55, 0.45, 0.85),
                digit: C(0.12, 0.10, 0.22),
                logo: C(0.93, 0.94, 0.99),
                bubbles: new[] { C(0.62, 0.74, 0.95), C(0.78, 0.72, 0.96), C(0.94, 0.72, 0.88), C(0.72, 0.88, 0.92), C(0.69, 0.83, 0.78), C(0.96, 0.84, 0.70), C(0.95, 0.76, 0.74), C(0.84, 0.80, 0.95), C(0.74, 0.79, 0.96) }),
            new Theme("night", 300,
                background: C(0.09, 0.11, 0.20),
                accent: C(0.55, 0.62, 0.85),
                digit: C(0.10, 0.13, 0.26),
                logo: C(0.90, 0.92, 0.98),
                bubbles: new[] { C(0.78, 0.84, 0.94), C(0.72, 0.80, 0.92), C(0.68, 0.78, 0.86), C(0.74, 0.82, 0.80), C(0.80, 0.82, 0.74), C(0.86, 0.80, 0.78), C(0.84, 0.76, 0.86), C(0.76, 0.74, 0.90), C(0.70, 0.72, 0.88) }),
        };

        public static Theme Default => All[0];

        public static Theme? ById(string? id) => All.FirstOrDefault(t => t.Id == id);
    }
}
