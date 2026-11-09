#nullable enable
using System.Collections.Generic;
using System.Linq;

namespace TenGo.Core
{
    /// Les 26 succès, verbatim d'AchievementManager.swift. Récompenses
    /// volontairement modestes face au catalogue boutique (1 400 pièces).
    public static class AchievementCatalog
    {
        public static readonly IReadOnlyList<AchievementDefinition> All = Build();

        public static AchievementDefinition? ById(string id) => All.FirstOrDefault(a => a.Id == id);

        private static List<AchievementDefinition> Build()
        {
            var list = new List<AchievementDefinition>();
            foreach (int t in new[] { 5, 10, 15, 20, 25 })
                list.Add(new AchievementDefinition("chain" + t, AchievementCategory.Chain, t,
                    "achievement.chain.title", "achievement.chain.desc", 5 + t / 2, t, AchievementStat.BestChain));
            foreach (int t in new[] { 1, 5, 10, 25, 50 })
                list.Add(new AchievementDefinition("perfect" + t, AchievementCategory.Perfect, t,
                    "achievement.perfect.title", "achievement.perfect.desc", 10 + t / 2, t * 2, AchievementStat.PerfectTotal));
            foreach (int t in new[] { 1000, 5000, 10000, 20000, 50000 })
                list.Add(new AchievementDefinition("scoreZen" + t, AchievementCategory.Score, t,
                    "achievement.score_zen.title", "achievement.score_zen.desc", 15, 25, AchievementStat.ZenBest));
            foreach (int t in new[] { 1000, 3000, 6000, 10000 })
                list.Add(new AchievementDefinition("scoreRush" + t, AchievementCategory.Score, t,
                    "achievement.score_rush.title", "achievement.score_rush.desc", 15, 25, AchievementStat.RushBest));
            foreach (int t in new[] { 7, 14, 30, 100 })
                list.Add(new AchievementDefinition("streak" + t, AchievementCategory.Daily, t,
                    "achievement.streak.title", "achievement.streak.desc", 10 + t / 2, t, AchievementStat.StreakBest));
            foreach (int t in new[] { 10, 30, 100 })
                list.Add(new AchievementDefinition("dailyTotal" + t, AchievementCategory.Daily, t,
                    "achievement.daily_total.title", "achievement.daily_total.desc", 10 + t / 2, t, AchievementStat.DailyTotal));
            return list;
        }
    }
}
