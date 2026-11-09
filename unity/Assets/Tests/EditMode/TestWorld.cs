using System;
using TenGo.Core;

namespace TenGo.Tests
{
    /// Ensemble des dépôts branchés sur un même magasin en mémoire et une
    /// horloge figée (équivalent du DataStore temporaire des tests Android).
    internal sealed class TestWorld
    {
        public readonly MemorySaveStore Store = new MemorySaveStore();
        public readonly FixedClock Clock;
        public readonly EconomyRepository Economy;
        public readonly LevelRepository Level;
        public readonly PlayerStatsRepository Stats;
        public readonly GameStateRepository Games;
        public readonly StreakRepository Streak;
        public readonly MissionRepository Missions;
        public readonly AchievementRepository Achievements;
        public readonly PuzzleProgressRepository Puzzles;

        /// Par défaut : 10/09/2026 à midi UTC (clé du jour 20260910).
        public TestWorld(DateTimeOffset? now = null, IRng claimRng = null)
        {
            Clock = new FixedClock(now ?? new DateTimeOffset(2026, 9, 10, 12, 0, 0, TimeSpan.Zero));
            Economy = new EconomyRepository(Store);
            Level = new LevelRepository(Store);
            Stats = new PlayerStatsRepository(Store);
            Games = new GameStateRepository(Store);
            Streak = new StreakRepository(Store, Economy, Clock);
            Missions = new MissionRepository(Store, Economy, Level, Streak, Clock, claimRng ?? new SeededGenerator(1));
            Achievements = new AchievementRepository(Store, Economy, Level, Stats, Games, Streak);
            Puzzles = new PuzzleProgressRepository(Store, TestGrids.Catalog);
        }
    }

    /// Générateur figé : renvoie toujours la même valeur (FixedRng des tests Kotlin).
    internal sealed class FixedRng : IRng
    {
        private readonly ulong _value;
        public FixedRng(ulong value) { _value = value; }
        public ulong NextULong() => _value;
    }
}
