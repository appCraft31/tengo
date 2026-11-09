#nullable enable
using System.Collections.Generic;

namespace TenGo.Core
{
    /// Données des boosters : identifiants (clés de sauvegarde), prix, clés de texte.
    public static class Boosters
    {
        public static readonly IReadOnlyList<Booster> All = new[] { Booster.Hint, Booster.Shuffle, Booster.Hammer };

        /// `rawValue` Swift : `hint`, `shuffle`, `hammer`.
        public static string Id(this Booster b) => b switch
        {
            Booster.Hint => "hint",
            Booster.Shuffle => "shuffle",
            _ => "hammer",
        };

        public static Booster? FromId(string? id) => id switch
        {
            "hint" => Booster.Hint,
            "shuffle" => Booster.Shuffle,
            "hammer" => Booster.Hammer,
            _ => (Booster?)null,
        };

        /// Prix unitaire en pièces.
        public static int Price(this Booster b) => b switch
        {
            Booster.Hint => 30,
            Booster.Shuffle => 50,
            _ => 80,
        };

        /// Lot boutique : 5 unités au prix de 4.
        public static int BundleQuantity(this Booster b) => 5;
        public static int BundlePrice(this Booster b) => b.Price() * 4;

        /// Clés de texte (`booster.hint`, `booster.hint_desc`, `booster.hint_coach`).
        public static string TitleKey(this Booster b) => "booster." + b.Id();
        public static string DetailsKey(this Booster b) => "booster." + b.Id() + "_desc";
        public static string CoachKey(this Booster b) => "booster." + b.Id() + "_coach";
    }
}
