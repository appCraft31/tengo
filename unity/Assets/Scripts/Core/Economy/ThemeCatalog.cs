#nullable enable
using System.Collections.Generic;
using System.Linq;

namespace TenGo.Core
{
    /// Catalogue des thèmes, repris tel quel de ThemeManager.swift (palettes
    /// harmonisées et contrôlées en contraste). Le défaut en premier, puis
    /// l'ordre d'affichage de la boutique.
    public static class ThemeCatalog
    {
        private static RgbColor C(double r, double g, double b) => new RgbColor(r, g, b);

        public static readonly IReadOnlyList<Theme> All = new[]
        {
            new Theme("default", 0,
                background: C(0.97, 0.95, 0.92),
                accent: C(0.82, 0.95, 0.88),
                digit: C(0.25, 0.25, 0.25),
                logo: C(0.28, 0.28, 0.28),
                bubbles: new[] { C(1.00, 0.62, 0.62), C(1.00, 0.78, 0.62), C(1.00, 0.96, 0.62), C(0.67, 0.95, 0.75), C(0.62, 0.86, 1.00), C(0.80, 0.72, 1.00), C(1.00, 0.72, 0.86), C(0.75, 0.91, 0.75), C(0.72, 0.76, 1.00) }),
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
