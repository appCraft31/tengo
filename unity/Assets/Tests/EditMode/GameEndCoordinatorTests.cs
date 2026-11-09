using System.Linq;
using NUnit.Framework;
using TenGo.Core;

namespace TenGo.Tests
{
    /// Port de GameEndCoordinatorTest.kt (horloge figée au 10/09/2026, midi UTC).
    public class GameEndCoordinatorTests
    {
        private TestWorld _w;
        private GameEndCoordinator _coordinator;

        [SetUp]
        public void SetUp()
        {
            _w = new TestWorld();
            _coordinator = new GameEndCoordinator(_w.Games, _w.Economy, _w.Level, _w.Missions, _w.Stats, _w.Achievements,
                _w.Puzzles, _w.Streak, _w.Clock);
        }

        private static GameEndInput Input(GameMode mode, int score, bool won, int dayKey = 20260910) =>
            new GameEndInput(mode, score, won, 4, 5, dayKey,
                mode.Kind == GameModeKind.Puzzle ? TestGrids.Catalog.Level(1, mode.Index) : null);

        [Test]
        public void NormalGameCreditsEverything()
        {
            _coordinator.OnGameStarted(GameMode.Normal);
            Assert.AreEqual(1, _w.Streak.Current);
            var midGame = _coordinator.OnChainCommitted(GameMode.Normal, 5, 200);
            Assert.IsTrue(midGame.Any(a => a.Id == "chain5"));
            GameEndResult r = _coordinator.OnGameEnded(Input(GameMode.Normal, 1450, false));
            Assert.AreEqual(RecordStatus.NewRecord, r.Record);
            Assert.AreEqual(10, r.CoinsEarned);
            Assert.AreEqual(LevelCurve.XpForGame(5, 4, false), r.Xp?.XpGained);
            CollectionAssert.AreEqual(new[] { 1450 }, _w.Games.HighScores);
            Assert.AreEqual(NextSuggestion.NextDaily, r.Suggestion);
            Assert.IsTrue(r.NewAchievements.Any(a => a.Id == "scoreZen1000"));
            Assert.IsFalse(r.NewAchievements.Any(a => a.Id == "chain5"));
        }

        [Test]
        public void RecordStatusesAreComputedBeforeInsertion()
        {
            _w.Games.AddScore(1000);
            Assert.AreEqual(RecordStatus.NearMiss(40), _coordinator.OnGameEnded(Input(GameMode.Normal, 960, false)).Record);
            Assert.AreEqual(RecordStatus.Best(1000), _coordinator.OnGameEnded(Input(GameMode.Normal, 300, false)).Record);
            Assert.AreEqual(RecordStatus.NewRecord, _coordinator.OnGameEnded(Input(GameMode.Normal, 1000, false)).Record);
        }

        [Test]
        public void DailyRewardsOnlyOncePerDayWinOrLose()
        {
            GameEndResult first = _coordinator.OnGameEnded(Input(GameMode.Daily, 400, false));
            Assert.AreEqual(30, _w.Economy.Balance);
            Assert.AreEqual(LevelCurve.XpDailyChallenge, first.Xp?.XpGained);
            Assert.AreEqual(1, _w.Stats.TotalDailyCompletions);
            Assert.IsTrue(_w.Economy.IsDailyCompleted(20260910));
            GameEndResult second = _coordinator.OnGameEnded(Input(GameMode.Daily, 2000, true));
            Assert.AreEqual(30, _w.Economy.Balance);
            Assert.IsNull(second.Xp);
            Assert.AreEqual(1, _w.Stats.TotalDailyCompletions);
            Assert.AreEqual(NextSuggestion.NextRush, second.Suggestion);
            Assert.AreEqual(0, second.CoinsEarned);
        }

        [Test]
        public void RushUsesItsOwnBoard()
        {
            GameEndResult r = _coordinator.OnGameEnded(Input(GameMode.Rush, 2600, false));
            Assert.AreEqual(LevelCurve.XpRush, r.Xp?.XpGained);
            CollectionAssert.AreEqual(new[] { 2600 }, _w.Games.RushHighScores);
            Assert.IsEmpty(_w.Games.HighScores);
            Assert.AreEqual(1, _w.Stats.TotalRushGames);
            Assert.IsTrue(r.NewAchievements.Any(a => a.Id == "scoreRush1000"));
            Assert.AreEqual(RecordStatus.NewRecord, r.Record);
        }

        [Test]
        public void PuzzleRecordsStarsOnlyWhenSolved()
        {
            GameEndResult lost = _coordinator.OnGameEnded(Input(GameMode.Puzzle(1, 1), 20, false));
            Assert.AreEqual(0, lost.PuzzleStars);
            Assert.AreEqual(LevelCurve.XpPuzzleAttempt, lost.Xp?.XpGained);
            Assert.AreEqual(0, _w.Puzzles.Stars(1, 1));
            GameEndResult won = _coordinator.OnGameEnded(Input(GameMode.Puzzle(1, 1), 50, true));
            Assert.AreEqual(3, won.PuzzleStars);
            Assert.AreEqual(3, _w.Puzzles.Stars(1, 1));
            Assert.AreEqual(NextSuggestion.None, won.Suggestion);
            Assert.AreEqual(RecordStatus.None, won.Record);
        }

        [Test]
        public void DuelAndDemo()
        {
            GameEndResult duel = _coordinator.OnGameEnded(Input(GameMode.Duel(42L, null), 300, false));
            Assert.AreEqual(LevelCurve.XpDuel, duel.Xp?.XpGained);
            GameEndResult demo = _coordinator.OnGameEnded(Input(GameMode.Demo, 9999, true));
            Assert.IsNull(demo.Xp);
            Assert.IsEmpty(_w.Games.HighScores);
            Assert.IsEmpty(demo.NewAchievements);
        }

        [Test]
        public void RushBoardClearedCountsPerfect()
        {
            _coordinator.OnRushBoardCleared();
            Assert.AreEqual(1, _w.Stats.PerfectBoardsTotal);
        }

        // MARK: - Ajouts Unity

        /// Partie continuée : le score remplace le premier, les pièces sont
        /// complétées, pas d'XP ni de partie comptée en double.
        [Test]
        public void ContinuationOnlyCountsTheExtraPoints()
        {
            GameEndResult first = _coordinator.OnGameEnded(Input(GameMode.Normal, 240, false));
            Assert.AreEqual(7, first.CoinsEarned);
            int xpBefore = _w.Level.TotalXP;
            int chainsBefore = _w.Stats.TotalChainsMade;
            int balanceBefore = _w.Economy.Balance;
            var input = new GameEndInput(GameMode.Normal, 600, false, 9, 7, continuedFrom: first);
            GameEndResult r = _coordinator.OnGameEnded(input);
            CollectionAssert.AreEqual(new[] { 600 }, _w.Games.HighScores);
            Assert.AreEqual(10, r.CoinsEarned);
            Assert.AreEqual(balanceBefore + 3, _w.Economy.Balance);
            Assert.IsNull(r.Xp);
            Assert.AreEqual(xpBefore, _w.Level.TotalXP);
            Assert.AreEqual(chainsBefore, _w.Stats.TotalChainsMade);
            Assert.AreEqual(RecordStatus.NewRecord, r.Record);
        }

        /// Miroir, graine et première partie branchés : la fin de partie les alimente.
        [Test]
        public void MirrorSeedAndFirstRunAreFed()
        {
            var mirror = new MirrorRepository(_w.Store, _w.Clock);
            var seeds = new SeedRepository(_w.Store, _w.Clock);
            var firstRun = new FirstRunState(_w.Store, _w.Clock, null);
            firstRun.Bootstrap();
            var coordinator = new GameEndCoordinator(_w.Games, _w.Economy, _w.Level, _w.Missions, _w.Stats, _w.Achievements,
                _w.Puzzles, _w.Streak, _w.Clock, null, mirror, seeds, firstRun);
            long plantedAt = -1;
            coordinator.SeedPlanted += at => plantedAt = at;
            var tally = new MirrorTally();
            tally.Record(new[] { 2, 3, 5 }, 2.0, false, false);
            var input = new GameEndInput(GameMode.Normal, 300, false, 1, 5, mirrorTally: tally);
            GameEndResult r = coordinator.OnGameEnded(input);
            Assert.AreEqual(MirrorInsight.Discovered("2+3+5", 1), r.MirrorInsight);
            Assert.AreEqual(SeedOutcomeKind.Planted, r.SeedOutcome?.Kind);
            Assert.AreEqual(3, r.SeedPower);
            Assert.AreEqual(_w.Clock.Now.ToUnixTimeMilliseconds() + SeedRules.GrowthMs, plantedAt);
            Assert.IsTrue(r.WasFirstGame);
            Assert.IsFalse(firstRun.IsPending);
            Assert.AreEqual(1, mirror.GamesMeasured);
        }
    }
}
