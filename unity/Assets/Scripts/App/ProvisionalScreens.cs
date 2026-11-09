using TenGo.Core;
using TenGo.Rendering;
using UnityEngine;

namespace TenGo.App
{
    /// Écrans hors partie de l'étape 5 : menu de développement et écrans
    /// d'attente. L'étape 6 remplace cette fabrique (`AppRoot.Screens`).
    public sealed class ProvisionalScreens : IScreenFactory
    {
        public IAppScreen Menu(AppRoot app) => new DevMenuScreen(app);

        public IAppScreen PuzzleLevels(AppRoot app, int world)
        {
            var screen = new PlaceholderScreen(app, Loc.T(PuzzleCatalog.NameKey(world)), app.Router.ShowMenu);
            var levels = app.Services.Catalog.Levels(world);
            float y = 250;
            for (int i = 0; i < Mathf.Min(6, levels.Count); i++)
            {
                var level = levels[i];
                int stars = app.Services.Puzzles.Stars(level.World, level.Index);
                screen.AddButton("level" + level.Index, Loc.F("puzzle.level_label", level.Index) + "  " + new string('*', stars), y,
                    Look.BubbleColor(level.Index % 9 + 1), () => app.StartGame(GameLaunch.Puzzle(level.World, level.Index)));
                y -= 88;
            }
            return screen;
        }

        public IAppScreen Duel(AppRoot app)
        {
            var screen = new PlaceholderScreen(app, Loc.T("menu.duel"), app.Router.ShowMenu);
            screen.AddButton("newDuel", Loc.T("duel.start"), 200, Look.Accent, () =>
            {
                long seed = (long)(Platform.SystemRng.Create().NextULong() & 0x7FFF_FFFF_FFFF_FFFF);
                app.StartGame(GameLaunch.Duel(seed, null));
            });
            screen.AddButton("joinDuel", Loc.T("duel.answer"), 100, Look.BubbleColor(4),
                () => app.StartGame(GameLaunch.Duel(424242, "TESTAB")));
            return screen;
        }

        public IAppScreen Shop(AppRoot app, bool returnToGame)
        {
            var s = app.Services;
            var screen = new PlaceholderScreen(app, Loc.T("shop.title"), () =>
            {
                if (returnToGame && s.Games.Load() is SavedGame saved) app.StartGame(GameLaunch.Normal(saved, resuming: true));
                else app.Router.ShowMenu();
            });
            float y = 220;
            foreach (var b in new[] { Booster.Hint, Booster.Shuffle, Booster.Hammer })
            {
                var booster = b;
                screen.AddButton("buy_" + b.Id(), Loc.T(b.TitleKey()) + " — " + b.Price() + " ¤", y, Look.BubbleColor(2 + (int)b * 3),
                    () => s.Economy.PurchaseBooster(booster));
                y -= 88;
            }
            return screen;
        }

        public IAppScreen Tutorial(AppRoot app, bool firstRun)
        {
            app.Services.Settings.HasSeenTutorial = true;
            return new PlaceholderScreen(app, Loc.T("settings.replay_tutorial"), app.Router.ShowMenu);
        }
    }
}
