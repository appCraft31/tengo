#nullable enable
namespace TenGo.Core
{
    /// Statistiques cumulées sur toute la vie de l'app (PlayerStatsManager.swift).
    public sealed class PlayerStatsRepository
    {
        private readonly ISaveStore _store;

        public PlayerStatsRepository(ISaveStore store)
        {
            _store = store;
        }

        /// Plus longue chaîne jamais réalisée, tous modes confondus.
        public int BestChainEver => _store.GetInt(SaveKeys.StatsBestChain);
        /// Grilles entièrement vidées (normal + Rush).
        public int PerfectBoardsTotal => _store.GetInt(SaveKeys.StatsPerfectTotal);
        /// Chaînes validées, tous modes confondus.
        public int TotalChainsMade => _store.GetInt(SaveKeys.StatsTotalChains);
        public int TotalRushGames => _store.GetInt(SaveKeys.StatsTotalRushGames);
        public int TotalDailyCompletions => _store.GetInt(SaveKeys.StatsTotalDaily);

        public PlayerStatsSnapshot Snapshot =>
            new PlayerStatsSnapshot(BestChainEver, PerfectBoardsTotal, TotalChainsMade, TotalRushGames, TotalDailyCompletions);

        /// Un chemin de `length` bulles vient d'être validé (hors démo).
        public void ReportChain(int length)
        {
            _store.SetInt(SaveKeys.StatsTotalChains, TotalChainsMade + 1);
            if (length > BestChainEver) _store.SetInt(SaveKeys.StatsBestChain, length);
        }

        /// Une grille vient d'être vidée (séparé de la fin de partie : en Rush,
        /// plusieurs grilles par partie).
        public void ReportPerfectBoard() => _store.SetInt(SaveKeys.StatsPerfectTotal, PerfectBoardsTotal + 1);

        /// Fin de partie. Les puzzles se comptent par leurs étoiles, pas ici.
        public void ReportGameEnded(GameModeKind mode, bool isPerfect)
        {
            if (isPerfect) ReportPerfectBoard();
            if (mode == GameModeKind.Rush) _store.SetInt(SaveKeys.StatsTotalRushGames, TotalRushGames + 1);
            else if (mode == GameModeKind.Daily) _store.SetInt(SaveKeys.StatsTotalDaily, TotalDailyCompletions + 1);
        }
    }
}
