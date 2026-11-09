#nullable enable
namespace TenGo.Core
{
    /// Événements analytics émis par la logique de partie. Noms et paramètres
    /// identiques à AnalyticsService.swift ; implémenté par le service Firebase
    /// (étape 7).
    public interface IGameAnalytics
    {
        /// `level_end` (mode, score, succès).
        void LevelEnd(string mode, int score, bool won);

        /// `daily_challenge_played`.
        void DailyChallengePlayed(int score);

        /// `mirror_insight_shown` (kind).
        void MirrorInsightShown(string kind);

        /// `seed_planted` (power, strengthened).
        void SeedPlanted(int power, bool strengthened);

        /// `first_game_start` (mode, seconds_since_first_open).
        void FirstGameStart(string mode, long secondsSinceFirstOpen);

        /// `first_game_complete` (mode, score, success, seconds_since_first_open).
        void FirstGameComplete(string mode, int score, bool won, long secondsSinceFirstOpen);
    }
}
