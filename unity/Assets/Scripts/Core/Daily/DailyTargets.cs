#nullable enable
using System;

namespace TenGo.Core
{
    /// Rotation hebdomadaire des cibles de difficulté.
    public static class DailyTargets
    {
        private static readonly DailyTarget[] Cycle =
        {
            DailyTarget.Easy, DailyTarget.Medium, DailyTarget.Hard, DailyTarget.Medium,
            DailyTarget.Easy, DailyTarget.Hard, DailyTarget.Medium,
        };

        /// `[E, M, H, M, E, H, M][abs(dayKey) % 7]`.
        public static DailyTarget ForDay(int dayKey) => Cycle[Math.Abs(dayKey) % Cycle.Length];
    }
}
