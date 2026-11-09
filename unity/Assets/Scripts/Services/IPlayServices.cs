using System;

namespace TenGo.Services
{
    /// Classements sur le jeu iOS (Game Center / Play Games).
    public enum Leaderboard { Normal, Daily, Rush }

    /// Services de plateforme : classements, demande d'avis, notifications
    /// locales, partage, mail au support. Réels : `NativePlayServices` ;
    /// éditeur et tests : `LogPlayServices`.
    public interface IPlayServices
    {
        /// Joueur connecté à Game Center / Play Games.
        bool IsSignedIn { get; }
        /// Nom public du joueur (pseudo Game Center / Play Games), `null` sinon.
        string PlayerName { get; }
        /// L'écran Classement montre ses boutons (iOS : seulement une fois
        /// Game Center connecté ; Android : toujours, la connexion se fait au toucher).
        bool ShowsLeaderboardButtons { get; }
        /// Changement de l'état de connexion.
        event Action SignInChanged;

        /// `GameCenterManager.submitScore` / `submitDailyScore` / `submitRushScore`.
        void SubmitScore(Leaderboard board, int score);
        /// Affiche un classement (`tenGOShowGameCenter`).
        void ShowLeaderboard(Leaderboard board);
        /// `SKStoreReviewController.requestReview` / Play In-App Review.
        void RequestReview();
        /// `NotificationManager.registerGameAndMaybeRequest` (compte les parties).
        void RegisterGameAndMaybeRequestNotifications();
        /// `NotificationManager.refreshIfAuthorized` (lancement de l'app).
        void RefreshNotifications();
        /// `DuelShare.present(code:)`.
        void ShareDuel(string code);
        /// Feuille de partage de l'app (`AppConfig.shareMessage`, réglages).
        void ShareApp();
        /// Mail au support (`mailto:` avec version et système).
        void OpenSupportMail();
        /// Page de l'app dans les réglages système (suivi, notifications).
        void OpenSystemSettings();
        /// Connexion Game Center / Play Games (`GameCenterManager.authenticate`).
        void Authenticate();
        /// Programme l'annonce d'éclosion de la graine (ms Unix).
        void ScheduleSeedBloom(long bloomAtMs);
        /// Annule l'annonce d'éclosion (fleur cueillie, `cancelSeedBloom`).
        void CancelSeedBloom();
    }
}
