#nullable enable
using System.Collections.Generic;
using System.Linq;

namespace TenGo.Core
{
    /// Succès (AchievementManager.swift). Seule la liste des succès déjà
    /// crédités est persistée ; la progression se recalcule depuis les autres
    /// référentiels (stats, scores, série).
    public sealed class AchievementRepository
    {
        private readonly ISaveStore _store;
        private readonly EconomyRepository _economy;
        private readonly LevelRepository _level;
        private readonly PlayerStatsRepository _stats;
        private readonly GameStateRepository _games;
        private readonly StreakRepository _streak;

        public AchievementRepository(ISaveStore store, EconomyRepository economy, LevelRepository level,
            PlayerStatsRepository stats, GameStateRepository games, StreakRepository streak)
        {
            _store = store;
            _economy = economy;
            _level = level;
            _stats = stats;
            _games = games;
            _streak = streak;
        }

        /// Identifiants déjà crédités.
        public IReadOnlyCollection<string> UnlockedIds =>
            new HashSet<string>(_store.GetStringList(SaveKeys.AchievementsUnlocked) ?? new List<string>());

        /// Valeur courante de la statistique suivie.
        public int CurrentValue(AchievementStat stat) => stat switch
        {
            AchievementStat.BestChain => _stats.BestChainEver,
            AchievementStat.PerfectTotal => _stats.PerfectBoardsTotal,
            AchievementStat.ZenBest => _games.BestScore,
            AchievementStat.RushBest => _games.RushBest,
            AchievementStat.StreakBest => _streak.Best,
            _ => _stats.TotalDailyCompletions,
        };

        public List<DisplayAchievement> Achievements(AchievementCategory? category = null) =>
            AchievementCatalog.All
                .Where(a => category == null || a.Category == category)
                .Select(a => new DisplayAchievement(a, CurrentValue(a.Stat)))
                .ToList();

        public int UnlockedCount => Achievements().Count(a => a.IsUnlocked);

        public int TotalCount => AchievementCatalog.All.Count;

        /// À appeler après toute mise à jour de statistiques : crédite pièces et
        /// XP des succès nouvellement atteints et les renvoie (toast). Idempotent.
        public List<AchievementDefinition> CheckForNewUnlocks()
        {
            var unlocked = (_store.GetStringList(SaveKeys.AchievementsUnlocked) ?? new List<string>()).ToList();
            var newly = new List<AchievementDefinition>();
            foreach (AchievementDefinition def in AchievementCatalog.All)
            {
                if (unlocked.Contains(def.Id) || CurrentValue(def.Stat) < def.Target) continue;
                unlocked.Add(def.Id);
                newly.Add(def);
                if (def.CoinReward > 0) _economy.AddCoins(def.CoinReward);
                if (def.XpReward > 0) _level.AwardForAchievement(def.XpReward);
            }
            if (newly.Count > 0) _store.SetStringList(SaveKeys.AchievementsUnlocked, unlocked);
            return newly;
        }
    }
}
