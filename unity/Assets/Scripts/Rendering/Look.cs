using TenGo.Core;
using UnityEngine;

namespace TenGo.Rendering
{
    /// Matières de bulles (`BubbleStyle.Kind` de CosmeticManager.swift).
    public enum BubbleMaterial { Classic, Matte, Glossy, Glass, Neon }

    /// Styles de tracé (`TrailStyle.Kind`).
    public enum TrailKind { Classic, Neon, Ribbon, Dotted, Ink }

    /// Apparence active : thème (`ThemeManager.shared.active`), matière de bulle
    /// et tracé (`CosmeticManager.shared`). L'étape 5 la règle depuis
    /// `ThemeRepository` et `CosmeticRepository` (`Apply`) ; les vues la lisent
    /// à leur création, comme les nœuds SpriteKit.
    public static class Look
    {
        public static Theme Theme { get; set; } = ThemeCatalog.Default;
        public static BubbleMaterial Bubble { get; set; } = BubbleMaterial.Classic;
        public static TrailKind Trail { get; set; } = TrailKind.Classic;

        public static Color Background => Colors.FromRgb(Theme.Background);
        public static Color Accent => Colors.FromRgb(Theme.Accent);
        public static Color Digit => Colors.FromRgb(Theme.Digit);
        public static Color Logo => Colors.FromRgb(Theme.Logo);

        /// Couleur d'une valeur (gris hors de 1-9, comme Swift).
        public static Color BubbleColor(int value) => Colors.FromRgb(Theme.ColorForValue(value));

        /// Règle l'apparence depuis les identifiants sauvegardés.
        public static void Apply(string themeId, string bubbleStyleId, string trailId)
        {
            Theme = ThemeCatalog.ById(themeId) ?? ThemeCatalog.Default;
            Bubble = ParseBubble(bubbleStyleId);
            Trail = ParseTrail(trailId);
        }

        public static BubbleMaterial ParseBubble(string id) => id switch
        {
            "matte" => BubbleMaterial.Matte,
            "glossy" => BubbleMaterial.Glossy,
            "glass" => BubbleMaterial.Glass,
            "neon" => BubbleMaterial.Neon,
            _ => BubbleMaterial.Classic,
        };

        public static TrailKind ParseTrail(string id) => id switch
        {
            "neon" => TrailKind.Neon,
            "ribbon" => TrailKind.Ribbon,
            "dotted" => TrailKind.Dotted,
            "ink" => TrailKind.Ink,
            _ => TrailKind.Classic,
        };

        /// Remet le thème par défaut et les styles « classic » (tests, captures).
        public static void Reset()
        {
            Theme = ThemeCatalog.Default;
            Bubble = BubbleMaterial.Classic;
            Trail = TrailKind.Classic;
        }
    }
}
