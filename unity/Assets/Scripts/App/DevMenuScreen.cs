using TenGo.Core;
using TenGo.Rendering;
using UnityEngine;

namespace TenGo.App
{
    /// Menu provisoire (en attendant le menu à onglets de l'étape 6) : un
    /// bouton par mode, plus « Continuer » si une partie normale est sauvegardée.
    public sealed class DevMenuScreen : SimpleScreen
    {
        public DevMenuScreen(AppRoot app) : base(app, "DevMenu")
        {
            var s = app.Services;
            Title("TEN•GO", 470, 64);
            TenGoText.Create(Root, "Menu provisoire (étape 5)", Fonts.AvenirMedium, 18, Look.Logo.WithAlpha(0.6f), Units.P(0, 415), 20);

            float y = 300;
            const float gap = 92;
            var size = new Vector2(420, 72);
            var saved = s.Games.Load();
            if (saved != null)
            {
                Button("continue", Loc.T("menu.continue"), new Vector2(0, y), size, Look.Accent,
                    () => app.StartGame(GameLaunch.Normal(saved)));
                y -= gap;
            }
            Button("normal", Loc.T("menu.new_game"), new Vector2(0, y), size, Look.BubbleColor(5),
                () => { s.Games.Clear(); app.StartGame(GameLaunch.Normal()); });
            y -= gap;
            Button("daily", Loc.T("menu.daily"), new Vector2(0, y), size, new Color(0.86f, 0.82f, 0.97f), () => app.StartGame(GameLaunch.ForDaily()));
            y -= gap;
            Button("rush", Loc.T("menu.rush"), new Vector2(0, y), size, new Color(1.0f, 0.89f, 0.78f), () => app.StartGame(GameLaunch.Rush()));
            y -= gap;
            Button("puzzle", Loc.T("menu.puzzles"), new Vector2(0, y), size, Look.BubbleColor(3), () => app.Router.ShowPuzzleLevels(1));
            y -= gap;
            Button("duel", Loc.T("menu.duel"), new Vector2(0, y), size, Look.BubbleColor(7), () => app.Router.ShowDuel());
            y -= gap;
            Button("demo", "Démo", new Vector2(0, y), size, null, () => app.StartGame(GameLaunch.Demo(7)));
            y -= gap * 0.9f;
            TenGoText.Create(Root, Loc.F("game_over.best_score", s.Games.BestScore) + "   ·   " + s.Economy.Balance + " ¤",
                Fonts.AvenirDemiBold, 18, Look.Logo.WithAlpha(0.7f), Units.P(0, y), 20).ShrinkToFit(600);
        }

        public override void Back() { }
    }
}
