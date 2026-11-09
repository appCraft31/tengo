namespace TenGo.App
{
    /// Fabrique des écrans hors partie : c'est ici que l'étape 6 branche les
    /// vrais écrans (menu à onglets, liste des puzzles, duel, boutique,
    /// tutoriel). `AppRoot.Screens` vaut `ProvisionalScreens` en attendant ;
    /// la partie ne passe que par `IScreenRouter`, sans connaître les écrans.
    public interface IScreenFactory
    {
        /// Accueil (`MenuScene`).
        IAppScreen Menu(AppRoot app);
        /// Liste des niveaux d'un monde (`PuzzleLevelsScene`), retour d'un puzzle.
        IAppScreen PuzzleLevels(AppRoot app, int world);
        /// Écran Duel (`DuelScene`), retour d'un duel.
        IAppScreen Duel(AppRoot app);
        /// Boutique (`BoutiqueScene`) ; `returnToGame` : la partie sauvegardée reprendra au retour.
        IAppScreen Shop(AppRoot app, bool returnToGame);
        /// Tutoriel (`TutorialScene`), depuis les réglages ou au premier lancement.
        IAppScreen Tutorial(AppRoot app, bool firstRun);
    }
}
