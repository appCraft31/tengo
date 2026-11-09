#nullable enable
using System.Collections.Generic;
using System.Linq;

namespace TenGo.Core
{
    /// Catalogues des cosmétiques, repris de CosmeticManager.swift (ordre boutique).
    public static class CosmeticCatalog
    {
        /// Style gratuit, toujours possédé.
        public const string DefaultId = "classic";

        public static readonly IReadOnlyList<CosmeticStyle> BubbleStyles = new[]
        {
            new CosmeticStyle(CosmeticKind.BubbleStyle, "classic", 0),
            new CosmeticStyle(CosmeticKind.BubbleStyle, "matte", 100),
            new CosmeticStyle(CosmeticKind.BubbleStyle, "glossy", 120),
            new CosmeticStyle(CosmeticKind.BubbleStyle, "glass", 150),
            new CosmeticStyle(CosmeticKind.BubbleStyle, "neon", 180),
        };

        public static readonly IReadOnlyList<CosmeticStyle> Trails = new[]
        {
            new CosmeticStyle(CosmeticKind.Trail, "classic", 0),
            new CosmeticStyle(CosmeticKind.Trail, "dotted", 100),
            new CosmeticStyle(CosmeticKind.Trail, "ink", 100),
            new CosmeticStyle(CosmeticKind.Trail, "ribbon", 120),
            new CosmeticStyle(CosmeticKind.Trail, "neon", 150),
        };

        public static IReadOnlyList<CosmeticStyle> Of(CosmeticKind kind) =>
            kind == CosmeticKind.BubbleStyle ? BubbleStyles : Trails;

        /// Style par identifiant, `classic` à défaut.
        public static CosmeticStyle ById(CosmeticKind kind, string? id) =>
            Of(kind).FirstOrDefault(s => s.Id == id) ?? Of(kind)[0];
    }
}
