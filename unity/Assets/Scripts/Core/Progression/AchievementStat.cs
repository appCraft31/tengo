#nullable enable
namespace TenGo.Core
{
    /// Statistique suivie par un succès (remplace les closures `currentValue` Swift).
    public enum AchievementStat
    {
        /// Plus longue chaîne jamais réalisée.
        BestChain,
        /// Grilles vidées au total.
        PerfectTotal,
        /// Meilleur score Zen.
        ZenBest,
        /// Meilleur score Rush.
        RushBest,
        /// Meilleure série.
        StreakBest,
        /// Défis du jour relevés au total.
        DailyTotal,
    }
}
