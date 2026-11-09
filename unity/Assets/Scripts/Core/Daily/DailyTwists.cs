#nullable enable
using System;

namespace TenGo.Core
{
    /// Règles des twists du défi du jour.
    public static class DailyTwists
    {
        /// Nombre d'éléments perturbateurs placés (intensité « corsée »).
        public static int Count(this DailyTwist twist) => twist switch
        {
            DailyTwist.Obstacles => 9,
            DailyTwist.Anchored => 6,
            DailyTwist.Frozen => 6,
            _ => 0,
        };

        /// Twist du jour : `allCases[abs(dayKey) % 3]`.
        public static DailyTwist ForDayKey(int dayKey) => (DailyTwist)(Math.Abs(dayKey) % 3);

        /// Nom technique (analytics, clés de texte) : `obstacles`, `anchored`, `frozen`.
        public static string Id(this DailyTwist twist) => twist switch
        {
            DailyTwist.Obstacles => "obstacles",
            DailyTwist.Anchored => "anchored",
            _ => "frozen",
        };
    }
}
