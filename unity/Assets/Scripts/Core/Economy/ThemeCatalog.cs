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
        /// Couleur `#RRGGBB`.
        private static RgbColor H(int rgb) => new RgbColor(((rgb >> 16) & 0xFF) / 255.0, ((rgb >> 8) & 0xFF) / 255.0, (rgb & 0xFF) / 255.0);

        public static readonly IReadOnlyList<Theme> All = new[]
        {
            // Refonte 4.0 (direction A « plat crème », design/refonte/systeme.css) :
            // fond crème, encre #34312C, accent #65BD90 et palette des bulles
            // calculée pour rester distincte en deutéranopie, protanopie et
            // tritanopie.
            new Theme("default", 0,
                background: H(0xF7F3EC),
                accent: H(0x65BD90),
                digit: H(0x34312C),
                logo: H(0x34312C),
                bubbles: new[] { H(0xFACFCC), H(0xDB9661), H(0xD7C06C), H(0xC6E99B), H(0x65BD90), H(0x7FEFFB), H(0x97C6F7), H(0xA99BE6), H(0xF0A5D7) }),
            // Thèmes payants recalculés pour la 4.0 (aplat) : 9 bulles distinctes
            // en vision normale, deutéranopie, protanopie et tritanopie (pire ΔE
            // OKLab ×100 de 7,1 à 9,8, contre 0,2 à 2,4 avant ; défaut : 5,1),
            // chiffre ≥ 4,5:1 sur chaque bulle, teintes bornées à l'identité du
            // thème. Voir MIGRATION.md, « Refonte visuelle (4.0) ».
            new Theme("forest", 150,
                background: H(0xEDF2E0), accent: H(0x97C99B), digit: H(0x242E1E), logo: H(0x284529),
                bubbles: new[] { H(0xD9A74C), H(0xBD8C2D), H(0xDCD069), H(0xE4F28F), H(0x8B9876), H(0x97C99B), H(0xA5FDD3), H(0x85E1DA), H(0x59B6B0) }),
            new Theme("ocean", 200,
                background: H(0xE8F5F7), accent: H(0x4EC4A6), digit: H(0x132D3E), logo: H(0x02435F),
                bubbles: new[] { H(0x59E5BB), H(0x9AFFE1), H(0x4EC4A6), H(0x6192E4), H(0xBBCEFF), H(0x8C95AD), H(0xA0B0E6), H(0xB48A57), H(0xCBA963) }),
            new Theme("desert", 200,
                background: H(0xF5E8D4), accent: H(0xC97E54), digit: H(0x3B2419), logo: H(0x613521),
                bubbles: new[] { H(0xFFC1DD), H(0xA38B93), H(0xC97E54), H(0xCFBAAD), H(0xF0B360), H(0xCBA040), H(0xFAEA89), H(0xA2A98B), H(0xCADAA2) }),
            new Theme("candy", 250,
                background: H(0xFAF0F2), accent: H(0xF46E73), digit: H(0x3B1F35), logo: H(0x592850),
                bubbles: new[] { H(0xF46E73), H(0xF5AC5E), H(0xF8E056), H(0xC0FAC1), H(0x7FD3B0), H(0x5EF0FD), H(0x9C8BFE), H(0xC4ACFF), H(0xC18ABB) }),
            new Theme("space", 300,
                background: H(0x17142E), accent: H(0xC6AEFF), digit: H(0x23193C), logo: H(0xEDF0FC),
                bubbles: new[] { H(0xFEA673), H(0xFFE0A4), H(0x7CC0BB), H(0xD3FBFC), H(0xAFDCDF), H(0x509DC0), H(0xA38FFF), H(0xC6AEFF), H(0xDB6891) }),
            new Theme("night", 300,
                background: H(0x171C33), accent: H(0x7CB8FF), digit: H(0x131F3B), logo: H(0xE6EBFA),
                bubbles: new[] { H(0x949192), H(0xC7C5C2), H(0xE1F6FA), H(0xE4B356), H(0xF0DDAD), H(0x65DFFF), H(0x7CB8FF), H(0x7B94E9), H(0xBCA1C3) }),
        };

        public static Theme Default => All[0];

        public static Theme? ById(string? id) => All.FirstOrDefault(t => t.Id == id);
    }
}
