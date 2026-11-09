namespace TenGo.Services
{
    /// Classements sur le jeu iOS (Game Center / Play Games).
    public enum Leaderboard { Normal, Daily, Rush }

    /// Services de plateforme que la partie sollicite (étape 7) : classements,
    /// demande d'avis, invitation aux notifications, partage d'un duel.
    public interface IPlayServices
    {
        /// `GameCenterManager.submitScore` / `submitDailyScore` / `submitRushScore`.
        void SubmitScore(Leaderboard board, int score);
        /// Affiche un classement (`tenGOShowGameCenter`).
        void ShowLeaderboard(Leaderboard board);
        /// `SKStoreReviewController.requestReview`.
        void RequestReview();
        /// `NotificationManager.registerGameAndMaybeRequest` (compte les parties).
        void RegisterGameAndMaybeRequestNotifications();
        /// `DuelShare.present(code:)`.
        void ShareDuel(string code);
        /// Programme l'annonce d'éclosion de la graine (ms Unix).
        void ScheduleSeedBloom(long bloomAtMs);
    }
}
