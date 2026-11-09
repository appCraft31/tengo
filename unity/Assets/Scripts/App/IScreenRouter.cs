namespace TenGo.App
{
    /// Navigation vue de la partie (`view?.presentScene(…, transition: fade(0.3))`).
    public interface IScreenRouter
    {
        void ShowMenu();
        void ShowPuzzleLevels(int world);
        void ShowDuel();
        void ShowShop(bool returnToGame);
        void ShowTutorial();
        /// Lance (ou relance) une partie avec un fondu.
        void StartGame(GameLaunch launch);
    }
}
