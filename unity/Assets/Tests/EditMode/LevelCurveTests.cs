using NUnit.Framework;
using TenGo.Core;

namespace TenGo.Tests
{
    /// Port de LevelCurveTest.kt (clés de titre au format iOS `level.title.*`).
    public class LevelCurveTests
    {
        [Test]
        public void CurveAndTitles()
        {
            Assert.AreEqual(60, LevelCurve.XpRequired(1));
            Assert.AreEqual(72, LevelCurve.XpRequired(2));
            Assert.AreEqual(new LevelBreakdown(1, 59, 60), LevelCurve.Breakdown(59));
            Assert.AreEqual(new LevelBreakdown(2, 0, 72), LevelCurve.Breakdown(60));
            Assert.AreEqual(new LevelBreakdown(3, 1, 84), LevelCurve.Breakdown(60 + 72 + 1));
            Assert.AreEqual(100, LevelCurve.LevelFor(10_000_000));
            Assert.AreEqual("level.title.beginner", LevelCurve.TitleKey(1));
            Assert.AreEqual("level.title.beginner", LevelCurve.TitleKey(4));
            Assert.AreEqual("level.title.learner", LevelCurve.TitleKey(5));
            Assert.AreEqual("level.title.master", LevelCurve.TitleKey(74));
            Assert.AreEqual("level.title.ten_master", LevelCurve.TitleKey(100));
        }

        [Test]
        public void GameXpFormula()
        {
            Assert.AreEqual(10, LevelCurve.XpForGame(2, 0, false));
            Assert.AreEqual(10 + 5 + 3, LevelCurve.XpForGame(5, 3, false));
            Assert.AreEqual(10 + 15 + 25 + 50, LevelCurve.XpForGame(12, 40, true));
        }

        [Test]
        public void RepositoryAwardsAndLevelsUp()
        {
            var repo = new LevelRepository(new MemorySaveStore());
            GainResult a = repo.Award(59);
            Assert.IsFalse(a.LeveledUp);
            GainResult b = repo.AwardForDuel();
            Assert.IsTrue(b.LeveledUp);
            Assert.AreEqual(1, b.PreviousLevel);
            Assert.AreEqual(2, b.NewLevel);
            Assert.AreEqual(84, repo.TotalXP);
            Assert.AreEqual(75, repo.AwardForDailyChallenge(true).XpGained);
            Assert.AreEqual(10, repo.AwardForPuzzle(false).XpGained);
        }

        // MARK: - Ajouts Unity

        /// iOS : plus d'XP à viser au niveau maximum (`xpForNextLevel` = 0).
        [Test]
        public void MaxLevelHasNothingLeftToEarn()
        {
            LevelBreakdown max = LevelCurve.Breakdown(10_000_000);
            Assert.AreEqual(100, max.Level);
            Assert.AreEqual(0, max.XpForNext);
        }

        [Test]
        public void ZeroAwardWritesNothing()
        {
            var store = new MemorySaveStore();
            var repo = new LevelRepository(store);
            GainResult r = repo.Award(0);
            Assert.AreEqual(0, r.XpGained);
            Assert.IsFalse(store.Has(SaveKeys.TotalXP));
        }
    }
}
