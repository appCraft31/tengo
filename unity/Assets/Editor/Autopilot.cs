using System;
using System.Collections.Generic;
using System.IO;
using TenGo.App;
using TenGo.Core;
using TenGo.Fx;
using TenGo.Game;
using TenGo.Rendering;
using TenGo.Services;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TenGo.Editor
{
    /// Pilote automatique (batchmode) : joue chaque mode comme un doigt (gestes
    /// injectés, chemins du solveur), l'horloge `FxTime` avancée à la main, et
    /// photographie les moments clés dans Logs/captures/play/.
    ///   Tools/unity.sh TenGo.Editor.Autopilot.Run
    /// Écran simulé : iPhone 17 Pro Max (celui des captures iOS de référence).
    public static class Autopilot
    {
        public const string Dir = "play";
        const float Dt = 1f / 60f;

        static AppRoot _app;
        static readonly List<string> Report = new();

        // MARK: - Lancement

        public static void Run()
        {
            Report.Clear();
            try
            {
                Normal();
                Coach();
                Daily();
                Rush();
                Puzzle();
                Duel();
                Demo();
                SecondChance();
            }
            finally
            {
                Teardown();
                ProjectSetup.ClearDynamicFontData();
            }
            foreach (var line in Report) Debug.Log("[Autopilot] " + line);
        }

        /// Application neuve sur une sauvegarde en mémoire, horloge fixe, sans menu.
        static AppRoot Boot(ISaveStore store = null, Action<ISaveStore> seed = null)
        {
            Teardown();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            FxTime.Reset();
            JuiceSettings.Reset();
            JuiceSettings.ForceFull = true;
            store ??= new MemorySaveStore();
            // Joueur déjà installé : tutoriel vu, quelques parties (pas de première session).
            store.SetBool(SaveKeys.HasSeenTutorial, true);
            store.SetInt(SaveKeys.NotifGamesPlayed, 5);
            seed?.Invoke(store);
            _app = AppRoot.Create(new AppRoot.Options
            {
                Store = store,
                Clock = new FixedClock(new DateTimeOffset(2026, 11, 9, 10, 0, 0, TimeSpan.FromHours(1)), TimeZoneInfo.Utc),
                Geometry = SceneGeometry.IPhone17ProMax,
                AutoTick = false,
                ReadDevices = false,
                Language = "fr",
                ShowMenuOnBoot = false,
                Fades = false,
                Services = new AppServices.Overrides
                {
                    Analytics = new LogAnalytics { Echo = false },
                    Interstitials = new SimulatedInterstitialAds { Ready = false },
                },
            });
            return _app;
        }

        static void Teardown()
        {
            if (_app != null) UnityEngine.Object.DestroyImmediate(_app.gameObject);
            _app = null;
            JuiceSettings.Reset();
            Look.Reset();
            FxTime.Reset();
        }

        // MARK: - Pilotage

        public static void Step()
        {
            FxTime.Step(Dt);
            _app.Tick(Dt);
        }

        public static void Advance(float seconds)
        {
            int n = Mathf.Max(1, Mathf.RoundToInt(seconds / Dt));
            for (int i = 0; i < n; i++) Step();
        }

        static GameController Game => _app.Game;

        /// Attend que la partie rende la main (ou que le panneau de fin s'affiche).
        static void WaitIdle(float max = 6f)
        {
            float t = 0;
            while (t < max && Game != null && Game.Animating && !Game.IsGameOver) { Step(); t += Dt; }
        }

        static void Tap(Vector2 pt)
        {
            _app.Input.InjectDown(pt);
            Step();
            _app.Input.InjectUp(pt);
            Step();
        }

        /// Trace `path` comme un doigt ; `release` faux : le doigt reste posé.
        static void Drag(IReadOnlyList<Cell> path, bool release = true, int stepsPerCell = 4)
        {
            var from = GameController.CellCenter(path[0]);
            _app.Input.InjectDown(from);
            Step();
            for (int i = 1; i < path.Count; i++)
            {
                var to = GameController.CellCenter(path[i]);
                for (int k = 1; k <= stepsPerCell; k++)
                {
                    _app.Input.InjectMove(Vector2.Lerp(from, to, k / (float)stepsPerCell));
                    Step();
                }
                from = to;
            }
            if (release)
            {
                _app.Input.InjectUp(from);
                Step();
            }
        }

        /// Joue le meilleur coup « vitrine » ; faux s'il n'y en a pas.
        static bool PlayOne(int maxLen = 5)
        {
            WaitIdle();
            if (Game == null || Game.IsGameOver) return false;
            var path = Game.Grid.ShowcasePath(maxLen);
            if (path == null || path.Count < 2) return false;
            Drag(path);
            Advance(0.3f);
            WaitIdle();
            return true;
        }

        /// Joue jusqu'à la fin de partie (ou `maxMoves`), puis laisse le panneau s'installer.
        static int PlayToEnd(int maxMoves = 120, int maxLen = 5)
        {
            int moves = 0;
            while (moves < maxMoves && Game != null && !Game.IsGameOver)
            {
                if (!PlayOne(maxLen)) { Advance(0.5f); if (!Game.IsGameOver) Advance(1.5f); break; }
                moves++;
            }
            float t = 0;
            while (Game != null && !Game.IsGameOver && t < 4f) { Step(); t += Dt; }
            return moves;
        }

        static string Shot(string name)
        {
            Directory.CreateDirectory(Path.Combine(Captures.Dir, Dir));
            var path = Captures.Shot(_app.Camera, $"{Dir}/{name}.png", Captures.W, Captures.H);
            Debug.Log("[Capture] " + path);
            return path;
        }

        static void Start(GameLaunch launch)
        {
            _app.Show(_app.CreateGame(launch), fade: false);
            Step();
        }

        // MARK: - Modes

        static void Normal()
        {
            Boot(seed: s => s.SetIntList(SaveKeys.HighScores, new List<int> { 2400, 1500 }));
            Start(GameLaunch.Normal());
            Advance(1.0f);
            Shot("normal_1_start");
            var path = Game.Grid.ShowcasePath(5);
            Drag(path, release: false);
            Advance(0.1f);
            Shot("normal_2_path");
            _app.Input.InjectUp(GameController.CellCenter(path[path.Count - 1]));
            Step();
            Advance(0.12f);
            Shot("normal_3_commit");
            Advance(0.6f);
            Shot("normal_4_flight");
            int moves = PlayToEnd();
            Advance(2.2f);
            Shot("normal_5_end");
            Report.Add($"Normal : {moves} coups, score {Game.Score}, fin {(Game.IsWin ? "grille vidée" : "bloquée")}, bilans {Game.GameEndsRecorded}");
        }

        /// Coach-mark d'un booster fraîchement possédé, puis marteau armé.
        static void Coach()
        {
            Boot(seed: s =>
            {
                s.SetInt(SaveKeys.BoosterInventory(Booster.Hint.Id()), 2);
                s.SetInt(SaveKeys.BoosterInventory(Booster.Shuffle.Id()), 2);
                s.SetInt(SaveKeys.BoosterInventory(Booster.Hammer.Id()), 2);
            });
            Start(GameLaunch.Normal());
            Advance(1.0f);
            Shot("normal_6_coach");
            // Ferme les trois coach-marks.
            for (int i = 0; i < 3 && Game.CoachVisible; i++) { Tap(Vector2.zero); Advance(0.4f); }
            if (Game.BoosterPoint(Booster.Hint) is Vector2 hint) { Tap(hint); Advance(0.3f); }
            Shot("normal_7_hint");
            Advance(1.2f);
            Report.Add($"Boosters : indice restant {_app.Services.Economy.BoosterCount(Booster.Hint)}, coach vu {_app.Services.Economy.BoosterCoachSeen(Booster.Hint)}");
        }

        static void Daily()
        {
            Boot();
            Start(GameLaunch.ForDaily());
            Advance(1.0f);
            Shot("daily_1_start");
            int moves = PlayToEnd();
            Advance(2.2f);
            Shot("daily_2_end");
            Report.Add($"Défi du jour : {moves} coups, score {Game.Score}");
        }

        static void Rush()
        {
            Boot(seed: s => s.SetIntList(SaveKeys.RushHighScores, new List<int> { 3000 }));
            Start(GameLaunch.Rush());
            Advance(0.7f);
            Shot("rush_1_countdown");
            Advance(2.6f);
            Shot("rush_2_start");
            int moves = 0;
            double guard = 0;
            while (!Game.IsGameOver && guard < 75)
            {
                if (PlayOne(7)) moves++; else Advance(0.2f);
                guard += 1;
                if (moves == 6) Shot("rush_3_playing");
            }
            // Le chrono continue à courir même sans coup.
            float t = 0;
            while (!Game.IsGameOver && t < 70) { Step(); t += Dt; }
            Advance(1.5f);
            Shot("rush_4_end");
            Report.Add($"Rush : {moves} coups, score {Game.Score}, chrono {Game.RushTimeRemaining:0.0}");
        }

        static void Puzzle()
        {
            Boot();
            Start(GameLaunch.Puzzle(1, 3));
            Advance(1.0f);
            Shot("puzzle_1_start");
            var plan = BoardSolver.SolveToEmpty(Game.Grid);
            int moves = 0;
            if (plan != null)
                foreach (var p in plan) { WaitIdle(); Drag(p); Advance(0.3f); moves++; }
            else moves = PlayToEnd();
            float t = 0;
            while (!Game.IsGameOver && t < 5) { Step(); t += Dt; }
            Advance(2.2f);
            Shot("puzzle_2_end");
            Report.Add($"Puzzle 1-3 : {moves} coups, score {Game.Score}, étoiles {Game.LastResult?.PuzzleStars}");
        }

        static void Duel()
        {
            Boot();
            Start(GameLaunch.Duel(20261109, null));
            Advance(1.0f);
            Shot("duel_1_start");
            int moves = PlayToEnd();
            Advance(2.5f);
            Shot("duel_2_end");
            Report.Add($"Duel : {moves} coups, score {Game.Score}, partage {(Game.PanelButtonPoint("duelShareBtn") != null ? "oui" : "non")}");
        }

        static void Demo()
        {
            Boot();
            Start(GameLaunch.Demo(7));
            Advance(2.4f);
            Shot("demo_1_playing");
            Advance(5f);
            Shot("demo_2_later");
            Report.Add($"Démo : score {Game.Score} après 7,4 s");
        }

        /// Fin de partie avec l'offre « Continuer » (vidéo simulée), puis reprise.
        static void SecondChance()
        {
            Boot();
            Start(GameLaunch.Normal());
            Advance(1.0f);
            for (int i = 0; i < 4; i++) PlayOne();
            Game.QaForceGameOver(1840, 7, 34, allowSecondChance: true);
            Advance(2.2f);
            Shot("normal_8_continue");
            if (Game.PanelButtonPoint("continueBtn") is Vector2 p)
            {
                Tap(p);
                Advance(1.0f);
                Shot("normal_9_continued");
            }
            Report.Add($"Seconde chance : continuée {(!Game.IsGameOver)}, bilans {Game.GameEndsRecorded}");
        }
    }
}
