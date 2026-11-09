#nullable enable
using System.Collections.Generic;

namespace TenGo.Core
{
    /// Thème visuel achetable (ThemeManager.swift), en données pures.
    public sealed class Theme
    {
        public const string DefaultId = "default";

        public Theme(string id, int price, RgbColor background, RgbColor accent, RgbColor digit, RgbColor logo,
            IReadOnlyList<RgbColor> bubbles)
        {
            Id = id;
            Price = price;
            Background = background;
            Accent = accent;
            Digit = digit;
            Logo = logo;
            Bubbles = bubbles;
        }

        public string Id { get; }
        public int Price { get; }
        public RgbColor Background { get; }
        /// Bouton principal du menu.
        public RgbColor Accent { get; }
        /// Chiffre sur les bulles.
        public RgbColor Digit { get; }
        /// Titre et texte d'interface.
        public RgbColor Logo { get; }
        /// 9 couleurs, valeurs 1 → 9.
        public IReadOnlyList<RgbColor> Bubbles { get; }

        /// Clé de texte du nom (`theme.forest`).
        public string NameKey => "theme." + Id;

        public bool IsDefault => Id == DefaultId;

        public RgbColor ColorForValue(int value) =>
            value >= 1 && value <= Bubbles.Count ? Bubbles[value - 1] : RgbColor.Gray;
    }
}
