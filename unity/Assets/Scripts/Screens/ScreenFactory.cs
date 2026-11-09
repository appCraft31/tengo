using System;
using TenGo.App;
using UnityEngine;

namespace TenGo.Screens
{
    /// Les vrais écrans hors partie (étape 6).
    public sealed class ScreenFactory : IScreenFactory
    {
        public IAppScreen Menu(AppRoot app) => new MenuScreen(app);
        public IAppScreen PuzzleLevels(AppRoot app, int world) => new PuzzleLevelsScreen(app, world);
        public IAppScreen Duel(AppRoot app, string incomingCode = null) => new DuelScreen(app, incomingCode);
        public IAppScreen Shop(AppRoot app, bool returnToGame, bool guided = false) => new ShopScreen(app, returnToGame, guided);
        public IAppScreen Tutorial(AppRoot app, bool firstRun) => new TutorialScreen(app, firstRun);
        public IAppScreen Profile(AppRoot app) => new ProfileScreen(app);
        public IAppScreen Missions(AppRoot app) => new MissionsScreen(app);
        public IAppScreen Achievements(AppRoot app) => new AchievementsScreen(app);
        public IAppScreen Mirror(AppRoot app) => new MirrorScreen(app);
        public IAppScreen Leaderboard(AppRoot app) => new LeaderboardScreen(app);
    }

    /// Réglages complets (`SettingsOverlay`), en partie comme à l'accueil.
    public sealed class OverlayFactory : IOverlayFactory
    {
        public IGameOverlay Settings(AppRoot app, Transform parent, Action replayTutorial) =>
            new SettingsOverlay(app, parent, replayTutorial);
    }
}
