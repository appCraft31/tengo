using System.Linq;
using NUnit.Framework;
using TenGo.Core;

namespace TenGo.Tests
{
    /// Port d'AchievementRepositoryTest.kt.
    public class AchievementRepositoryTests
    {
        [Test]
        public void CatalogHas26WithExpectedRewards()
        {
            Assert.AreEqual(26, AchievementCatalog.All.Count);
            Assert.AreEqual(26, AchievementCatalog.All.Select(a => a.Id).Distinct().Count());
            AchievementDefinition chain25 = AchievementCatalog.ById("chain25");
            Assert.AreEqual(17, chain25.CoinReward);
            Assert.AreEqual(25, chain25.XpReward);
            AchievementDefinition perfect50 = AchievementCatalog.ById("perfect50");
            Assert.AreEqual(35, perfect50.CoinReward);
            Assert.AreEqual(100, perfect50.XpReward);
            AchievementDefinition streak100 = AchievementCatalog.ById("streak100");
            Assert.AreEqual(60, streak100.CoinReward);
            Assert.AreEqual(100, streak100.XpReward);
        }

        [Test]
        public void UnlocksOnceAndCredits()
        {
            var w = new TestWorld();
            AchievementRepository repo = w.Achievements;
            Assert.IsEmpty(repo.CheckForNewUnlocks());
            w.Stats.ReportChain(12);
            w.Games.AddScore(1500);
            w.Games.AddRushScore(3200);
            w.Stats.ReportGameEnded(GameModeKind.Daily, true);
            var fresh = repo.CheckForNewUnlocks().Select(a => a.Id).ToList();
            CollectionAssert.AreEquivalent(new[] { "chain5", "chain10", "perfect1", "scoreZen1000", "scoreRush1000", "scoreRush3000" }, fresh);
            Assert.AreEqual(7 + 10 + 10 + 15 + 15 + 15, w.Economy.Balance);
            Assert.AreEqual(5 + 10 + 2 + 25 + 25 + 25, w.Level.TotalXP);
            Assert.IsEmpty(repo.CheckForNewUnlocks());
            var chain = repo.Achievements(AchievementCategory.Chain);
            Assert.AreEqual(5, chain.Count);
            Assert.AreEqual(12, chain[0].CurrentValue);
            Assert.AreEqual(2, chain.Count(a => a.IsUnlocked));
        }
    }
}
