using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TenGo.App;
using TenGo.Core;
using TenGo.Fx;
using TenGo.Game;
using TenGo.Haptics;
using TenGo.Rendering;
using TenGo.Services;
using UnityEngine;

namespace TenGo.Tests
{
    /// La partie de l'étape 5 de bout en bout : `AppRoot` sur une sauvegarde en
    /// mémoire, gestes injectés, horloge avancée à la main dans un même pas de
    /// test (le `FxDriver` n'a pas la main entre deux appels).
    public class GameFlowTests
    {
        const float Dt = 1f / 60f;
        AppRoot _app;
        MemorySaveStore _store;
        LogAnalytics _analytics;
        SimulatedRewardedAds _rewarded;

        GameController Game => _app.Game;
        AppServices S => _app.Services;

        [SetUp]
        public void SetUp()
        {
            FxTime.Reset();
            JuiceSettings.Reset();
            JuiceSettings.SystemReduceMotion = () => false;
            Look.Reset();
            HapticManager.Current = new NullHaptics();
            _store = new MemorySaveStore();
            _store.SetBool(SaveKeys.HasSeenTutorial, true);
            _store.SetInt(SaveKeys.NotifGamesPlayed, 5);
            _analytics = new LogAnalytics { Echo = false };
            _rewarded = new SimulatedRewardedAds();
        }

        [TearDown]
        public void TearDown()
        {
            if (_app != null) UnityEngine.Object.DestroyImmediate(_app.gameObject);
            _app = null;
            FxTime.Reset();
            JuiceSettings.Reset();
            HapticManager.Reset();
            Look.Reset();
            Audio.SoundManager.ResetShared();
        }

        void Boot()
        {
            _app = AppRoot.Create(new AppRoot.Options
            {
                Store = _store,
                Clock = new FixedClock(new DateTimeOffset(2026, 11, 9, 10, 0, 0, TimeSpan.Zero), TimeZoneInfo.Utc),
                Geometry = SceneGeometry.IPhone17ProMax,
                AutoTick = false,
                ReadDevices = false,
                Language = "fr",
                ShowMenuOnBoot = false,
                Fades = false,
                Services = new AppServices.Overrides
                {
                    Analytics = _analytics,
                    Interstitials = new SimulatedInterstitialAds { Ready = false },
                    Rewarded = _rewarded,
                },
            });
        }

        void Start(GameLaunch launch)
        {
            if (_app == null) Boot();
            _app.Show(_app.CreateGame(launch), fade: false);
            Advance(1f);
        }

        void Step() { FxTime.Step(Dt); _app.Tick(Dt); }

        void Advance(float s) { for (int i = 0, n = Mathf.CeilToInt(s / Dt); i < n; i++) Step(); }

        void WaitIdle(float max = 6f)
        {
            for (float t = 0; t < max && Game.Animating && !Game.IsGameOver; t += Dt) Step();
        }

        void Tap(Vector2 pt)
        {
            _app.Input.InjectDown(pt); Step();
            _app.Input.InjectUp(pt); Step();
        }

        void Drag(IReadOnlyList<Cell> path, bool release = true)
        {
            var from = Game.CellCenter(path[0]);
            _app.Input.InjectDown(from); Step();
            for (int i = 1; i < path.Count; i++)
            {
                var to = Game.CellCenter(path[i]);
                for (int k = 1; k <= 4; k++) { _app.Input.InjectMove(Vector2.Lerp(from, to, k / 4f)); Step(); }
                from = to;
            }
            if (release) { _app.Input.InjectUp(from); Step(); }
        }

        bool PlayOne()
        {
            WaitIdle();
            if (Game.IsGameOver) return false;
            var path = Game.Grid.ShowcasePath(5);
            if (path == null) return false;
            Drag(path);
            Advance(0.3f);
            WaitIdle();
            return true;
        }

        void PlayToEnd(int maxMoves = 150)
        {
            for (int i = 0; i < maxMoves && !Game.IsGameOver; i++) if (!PlayOne()) break;
            for (float t = 0; t < 5 && !Game.IsGameOver; t += Dt) Step();
            Advance(2f);
        }

        static int CountBubbles(GridModel g) => g.Cells.Sum(row => row.Count(b => b != null));

        static SavedGame Seeded(ulong seed, int score = 0) => new(GridModel.Generate(new SeededGenerator(seed)), score);

        void DismissCoachMarks()
        {
            for (int i = 0; i < 5 && Game.CoachVisible; i++) { Tap(Vector2.zero); Advance(0.4f); }
        }

        // MARK: - Validation, rejet, gravité

        [Test]
        public void ValidPathScoresAndRemovesBubbles()
        {
            Start(GameLaunch.Normal(Seeded(42)));
            int before = CountBubbles(Game.Grid);
            var path = Game.Grid.ShowcasePath(5);
            Drag(path);
            Advance(1.5f);
            Assert.AreEqual(Scoring.ForPath(path.Count) * 1, Game.Score);
            Assert.AreEqual(before - path.Count, CountBubbles(Game.Grid));
            Assert.AreEqual(Game.Score, Game.DisplayedScore, "le compteur rattrape le score à l'arrivée de la bulle « +N »");
            Assert.AreEqual(1, Game.Combos);
        }

        [Test]
        public void ShortPathIsCancelledAndOverflowIsRefused()
        {
            Start(GameLaunch.Normal(Seeded(7)));
            var g = Game.Grid;
            // Deux voisines de somme < 10 : relâcher annule.
            Cell a = default, b = default;
            bool found = false;
            for (int r = 0; r < GameRules.Rows && !found; r++)
            for (int c = 0; c + 1 < GameRules.Cols && !found; c++)
                if (g.Cells[r][c].Value + g.Cells[r][c + 1].Value < 10) { a = new Cell(r, c); b = new Cell(r, c + 1); found = true; }
            Assert.IsTrue(found);
            int before = CountBubbles(g);
            Drag(new[] { a, b }, release: false);
            Assert.AreEqual(2, Game.CurrentPath.Count);
            _app.Input.InjectUp(Game.CellCenter(b)); Step();
            Advance(0.5f);
            Assert.AreEqual(0, Game.Score);
            Assert.AreEqual(before, CountBubbles(Game.Grid));
            Assert.AreEqual(0, Game.CurrentPath.Count);

            // Deux voisines de somme > 10 : la seconde est refusée.
            found = false;
            for (int r = 0; r < GameRules.Rows && !found; r++)
            for (int c = 0; c + 1 < GameRules.Cols && !found; c++)
                if (g.Cells[r][c].Value + g.Cells[r][c + 1].Value > 10) { a = new Cell(r, c); b = new Cell(r, c + 1); found = true; }
            if (!found) Assert.Inconclusive("pas de paire > 10 sur cette grille");
            Drag(new[] { a, b }, release: false);
            Assert.AreEqual(1, Game.CurrentPath.Count, "la bulle de trop est refusée");
            _app.Input.InjectCancel(); Step();
        }

        [Test]
        public void BacktrackShortensThePath()
        {
            Start(GameLaunch.Normal(Seeded(11)));
            var path = Game.Grid.ShowcasePath(5);
            Assume.That(path.Count >= 3);
            Drag(path.Take(3).ToList(), release: false);
            Assert.AreEqual(3, Game.CurrentPath.Count);
            _app.Input.InjectMove(Game.CellCenter(path[0])); Step();
            Assert.AreEqual(1, Game.CurrentPath.Count);
            _app.Input.InjectCancel(); Step();
        }

        [Test]
        public void GravityLeavesNoHoleAndViewsFollowTheModel()
        {
            Start(GameLaunch.Normal(Seeded(99)));
            for (int i = 0; i < 4; i++) PlayOne();
            Advance(1f);
            var g = Game.Grid;
            for (int c = 0; c < GameRules.Cols; c++)
            {
                bool hole = false;
                for (int r = 0; r < GameRules.Rows; r++)
                {
                    if (g.Blocked[r][c]) { hole = false; continue; }
                    if (g.Cells[r][c] == null) hole = true;
                    else Assert.IsFalse(hole && !g.Cells[r][c].IsAnchored, $"bulle flottante en ({r},{c})");
                    var node = Game.NodeAt(r, c);
                    Assert.AreEqual(g.Cells[r][c] != null, node != null, $"vue et modèle en ({r},{c})");
                    if (node != null)
                    {
                        Assert.AreEqual(g.Cells[r][c].Value, node.Value);
                        Assert.AreEqual(Game.CellCenter(new Cell(r, c)).y, node.transform.localPosition.y * 100f, 0.5f);
                    }
                }
            }
        }

        // MARK: - Boosters

        [Test]
        public void BoostersAreConsumedAndCoachMarksMarkedSeen()
        {
            _store.SetInt(SaveKeys.BoosterInventory(Booster.Shuffle.Id()), 1);
            _store.SetInt(SaveKeys.BoosterInventory(Booster.Hammer.Id()), 1);
            _store.SetInt(SaveKeys.BoosterInventory(Booster.Hint.Id()), 1);
            Start(GameLaunch.Normal(Seeded(5)));
            Assert.IsTrue(Game.CoachVisible, "coach-mark du premier booster possédé");
            DismissCoachMarks();
            Assert.IsTrue(S.Economy.BoosterCoachSeen(Booster.Hint));
            Assert.IsTrue(S.Economy.BoosterCoachSeen(Booster.Hammer));

            Tap(Game.BoosterPoint(Booster.Shuffle).Value);
            Advance(0.5f);
            Assert.AreEqual(0, S.Economy.BoosterCount(Booster.Shuffle));
            Assert.AreEqual(1, _analytics.Count("booster_used"));

            int before = CountBubbles(Game.Grid);
            Tap(Game.BoosterPoint(Booster.Hammer).Value);
            Assert.IsTrue(Game.HammerArmed);
            Tap(Game.CellCenter(new Cell(0, 0)));
            Advance(1f);
            Assert.AreEqual(0, S.Economy.BoosterCount(Booster.Hammer));
            Assert.AreEqual(before - 1, CountBubbles(Game.Grid));

            Tap(Game.BoosterPoint(Booster.Hint).Value);
            Advance(1.2f);
            Assert.AreEqual(0, S.Economy.BoosterCount(Booster.Hint));
            // Refonte 4.0 : les trois boosters restent affichés, stock vide signalé par « + ».
            CollectionAssert.AreEqual(new[] { Booster.Hint, Booster.Shuffle, Booster.Hammer }, Game.VisibleBoosters);
            Assert.AreEqual("+", Game.BoosterBadge(Booster.Hint));

            // Stock vide : le tap ne consomme rien et propose la boutique (modale « à court de boosters »).
            Advance(0.5f);
            Tap(Game.BoosterPoint(Booster.Shuffle).Value);
            Assert.IsTrue(Game.BoosterPromptVisible);
            Assert.AreEqual(2, _analytics.Count("booster_used"), "aucune consommation de plus");
        }

        // MARK: - Sauvegarde et reprise

        [Test]
        public void NormalGameIsSavedOnHomeAndResumedIdentically()
        {
            Start(GameLaunch.Normal(Seeded(21)));
            PlayOne();
            Advance(1f);
            int score = Game.Score;
            var cells = Game.Grid.Cells.Select(r => r.Select(b => b?.Value ?? 0).ToArray()).ToArray();
            Tap(Game.HomePoint);
            Advance(0.5f);
            var saved = S.Games.Load();
            Assert.IsNotNull(saved);
            Assert.AreEqual(score, saved.Score);
            Assert.IsNull(_app.Game, "retour au menu");

            Start(GameLaunch.Normal(saved));
            Assert.AreEqual(score, Game.Score);
            for (int r = 0; r < GameRules.Rows; r++)
            for (int c = 0; c < GameRules.Cols; c++)
                Assert.AreEqual(cells[r][c], Game.Grid.Cells[r][c]?.Value ?? 0);
        }

        // MARK: - Une partie complète par mode

        [Test]
        public void NormalGameEndsAndIsCreditedOnce()
        {
            Start(GameLaunch.Normal(Seeded(3)));
            PlayToEnd();
            Assert.IsTrue(Game.IsGameOver);
            Assert.AreEqual(1, Game.GameEndsRecorded);
            Assert.AreEqual(Game.Score, S.Games.HighScores[0]);
            int balance = S.Economy.Balance;
            Assert.GreaterOrEqual(balance, Game.LastResult.CoinsEarned, "pièces de la partie (+ missions et succès éventuels)");
            Advance(3f);
            Assert.AreEqual(1, Game.GameEndsRecorded, "un seul bilan par fin de partie");
            Assert.AreEqual(balance, S.Economy.Balance);
            Assert.AreEqual(1, S.Games.HighScores.Count);
            Assert.IsFalse(S.Games.Exists, "la partie terminée n'est plus à reprendre");
            Assert.AreEqual(1, _analytics.Count("level_end"));
        }

        [Test]
        public void SecondChanceContinuesWithoutDoubleCounting()
        {
            Start(GameLaunch.Normal(Seeded(8)));
            PlayOne();
            Game.QaForceGameOver(500, 4, 1, allowSecondChance: true);
            Advance(2f);
            Assert.IsTrue(Game.OffersContinue);
            Tap(Game.PanelButtonPoint("continueBtn").Value);
            Advance(1f);
            Assert.IsFalse(Game.IsGameOver);
            Assert.AreEqual(500, Game.Score);
            Game.QaForceGameOver(800, 4, 2);
            Advance(2f);
            Assert.AreEqual(1, S.Games.HighScores.Count, "la partie continuée remplace son score");
            Assert.AreEqual(800, S.Games.HighScores[0]);
            Assert.IsFalse(Game.OffersContinue, "une seule seconde chance");
        }

        [Test]
        public void DailyGameCompletesTheDay()
        {
            Start(GameLaunch.ForDaily());
            var today = DayKeys.Challenge(S.Clock);
            PlayToEnd();
            Assert.IsTrue(Game.IsGameOver);
            Assert.IsTrue(S.Economy.IsDailyCompleted(today));
            Assert.AreEqual(1, _analytics.Count("daily_challenge_played"));
            Assert.IsNull(Game.PanelButtonPoint("replayBtn"), "pas de rejeu du défi");
            Assert.IsNotNull(Game.PanelButtonPoint("dailyLeaderboardBtn"));
        }

        [Test]
        public void RushCountsDownThenEndsOnTime()
        {
            Start(GameLaunch.Rush());
            Assert.IsTrue(Game.RushCountdownActive);
            Advance(2.5f);
            Assert.IsFalse(Game.RushCountdownActive);
            for (int i = 0; i < 3; i++) PlayOne();
            double remaining = Game.RushTimeRemaining;
            Assert.Less(remaining, 60);
            Game.QaEndRush();
            Advance(1f);
            Assert.IsTrue(Game.IsGameOver);
            Assert.AreEqual(Game.Score, S.Games.RushHighScores[0]);
            Assert.AreEqual(0, S.Games.HighScores.Count);
        }

        [Test]
        public void RushTimerRunsOutByItself()
        {
            Start(GameLaunch.Rush());
            Advance(64f);
            Assert.IsTrue(Game.IsGameOver);
            Assert.AreEqual(1, Game.GameEndsRecorded);
        }

        [Test]
        public void PuzzleIsSolvedWithStars()
        {
            Start(GameLaunch.Puzzle(1, 1));
            var plan = BoardSolver.SolveToEmpty(Game.Grid);
            Assert.IsNotNull(plan, "le niveau 1-1 est solvable");
            foreach (var p in plan) { WaitIdle(); Drag(p); Advance(0.3f); }
            for (float t = 0; t < 4 && !Game.IsGameOver; t += Dt) Step();
            Advance(1.5f);
            Assert.IsTrue(Game.IsGameOver);
            Assert.IsTrue(Game.IsWin);
            Assert.Greater(S.Puzzles.Stars(1, 1), 0);
            Assert.IsNotNull(Game.PanelButtonPoint("nextPuzzleBtn"));
        }

        [Test]
        public void DuelPublishesItsResult()
        {
            Start(GameLaunch.Duel(123456, null));
            PlayToEnd();
            Advance(1f);
            Assert.IsTrue(Game.IsGameOver);
            Assert.AreEqual(1, S.DuelHistory.Codes.Count);
            Assert.IsNotNull(Game.PanelButtonPoint("duelShareBtn"), "le challenger repart avec le code à partager");
            Assert.AreEqual(0, S.Games.HighScores.Count);
        }

        [Test]
        public void DemoPlaysAloneAndRecordsNothing()
        {
            Start(GameLaunch.Demo(7));
            Advance(6f);
            Assert.Greater(Game.Score, 0);
            Assert.AreEqual(0, S.Games.HighScores.Count);
            Assert.AreEqual(0, _analytics.Count("level_end"));
        }
    }
}
