namespace TenGo.App
{
    /// Fabrique des écrans hors partie (`TenGo.Screens.ScreenFactory`) : la
    /// partie ne passe que par `IScreenRouter`, sans connaître les écrans.
    public interface IScreenFactory
    {
        /// Accueil (`MenuScene`, onglet Jouer).
        IAppScreen Menu(AppRoot app);
        /// Liste des niveaux d'un monde (`PuzzleLevelsScene`), retour d'un puzzle.
        IAppScreen PuzzleLevels(AppRoot app, int world);
        /// Écran Duel (`DuelScene`, onglet Social) ; `incomingCode` : code reçu par lien.
        IAppScreen Duel(AppRoot app, string incomingCode = null);
        /// Boutique (`BoutiqueScene`, onglet Boutique) ; `returnToGame` : la partie
        /// sauvegardée reprendra au retour ; `guided` : tutoriel du premier achat.
        IAppScreen Shop(AppRoot app, bool returnToGame, bool guided = false);
        /// Tutoriel (`TutorialScene`), depuis les réglages ou au premier lancement.
        IAppScreen Tutorial(AppRoot app, bool firstRun);
        /// Progression (`ProfileScene`, onglet Progression).
        IAppScreen Profile(AppRoot app);
        IAppScreen Missions(AppRoot app);
        IAppScreen Achievements(AppRoot app);
        /// « Ton cerveau » (`MirrorScene`).
        IAppScreen Mirror(AppRoot app);
        /// Classements (`LeaderboardScene`).
        IAppScreen Leaderboard(AppRoot app);
    }
}
