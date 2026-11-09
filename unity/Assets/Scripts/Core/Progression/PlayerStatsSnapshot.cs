#nullable enable
namespace TenGo.Core
{
    /// Statistiques cumulées du joueur à un instant donné.
    public readonly struct PlayerStatsSnapshot
    {
        public PlayerStatsSnapshot(int bestChainEver, int perfectBoardsTotal, int totalChainsMade, int totalRushGames, int totalDailyCompletions)
        {
            BestChainEver = bestChainEver;
            PerfectBoardsTotal = perfectBoardsTotal;
            TotalChainsMade = totalChainsMade;
            TotalRushGames = totalRushGames;
            TotalDailyCompletions = totalDailyCompletions;
        }

        public int BestChainEver { get; }
        public int PerfectBoardsTotal { get; }
        public int TotalChainsMade { get; }
        public int TotalRushGames { get; }
        public int TotalDailyCompletions { get; }
    }
}
