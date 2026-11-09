using System.Linq;
using NUnit.Framework;
using TenGo.Core;

namespace TenGo.Tests
{
    /// Port de MissionRepositoryTest.kt.
    public class MissionRepositoryTests
    {
        private TestWorld _w;

        /// Générateur figé à 0 : la super mission accorde toujours un bouclier.
        [SetUp]
        public void SetUp() => _w = new TestWorld(claimRng: new FixedRng(0));

        [Test]
        public void RollIsDeterministicAndHasThreeRegularPlusOneSuper()
        {
            var a = MissionRepository.RollFor(20260910);
            var b = MissionRepository.RollFor(20260910);
            CollectionAssert.AreEqual(a, b);
            Assert.AreEqual(4, a.Count);
            Assert.AreEqual(3, a.Count(e => MissionCatalog.Regular.Any(d => d.Id == e.Id)));
            Assert.AreEqual(1, a.Count(e => MissionCatalog.SuperMissions.Any(d => d.Id == e.Id)));
            Assert.AreEqual(4, a.Select(e => e.Id).Distinct().Count());
        }

        [Test]
        public void RolloverIsStrictlyForward()
        {
            MissionRepository r = _w.Missions;
            r.EnsureRolled(20260910);
            var first = r.TodaysMissions.Select(m => m.Definition.Id).ToList();
            r.ReportMove();
            r.EnsureRolled(20260909);
            CollectionAssert.AreEqual(first, r.TodaysMissions.Select(m => m.Definition.Id).ToList());
            if (first.Any(id => id.StartsWith("moves")))
                Assert.AreEqual(1, r.TodaysMissions.First(m => m.Definition.Kind == MissionKind.MovesPlayed).Progress);
            r.EnsureRolled(20260911);
            Assert.IsTrue(r.TodaysMissions.All(m => m.Progress == 0 && !m.Claimed));
        }

        [Test]
        public void ProgressIsClampedAndClaimIsUnique()
        {
            MissionRepository r = _w.Missions;
            r.EnsureRolled(20260910);
            for (int i = 0; i < 50; i++)
            {
                r.ReportChain(20);
                r.ReportMove();
                r.ReportScore(2000);
            }
            r.ReportGameEnded(true);
            var missions = r.TodaysMissions;
            foreach (DisplayMission m in missions) Assert.LessOrEqual(m.Progress, m.Definition.Target);
            Assert.IsTrue(r.HasUnclaimedReward);
            DisplayMission completed = missions.First(m => m.IsCompleted);
            int before = _w.Economy.Balance;
            Assert.IsNotNull(r.Claim(completed.Definition.Id));
            Assert.AreEqual(before + completed.Definition.CoinReward, _w.Economy.Balance);
            Assert.IsNull(r.Claim(completed.Definition.Id));
            DisplayMission superOne = r.TodaysMissions.First(m => m.Definition.IsSuper);
            if (superOne.IsCompleted)
            {
                MissionClaimResult res = r.Claim(superOne.Definition.Id);
                Assert.IsTrue(res.ShieldGranted);
                Assert.AreEqual(1, _w.Streak.ShieldCount);
                Assert.AreEqual(LevelCurve.XpSuperMission, res.Xp.XpGained);
            }
            r.ReportMove();
            Assert.IsTrue(r.TodaysMissions.First(m => m.Definition.Id == completed.Definition.Id).Claimed);
        }

        // MARK: - Ajouts Unity

        /// Comme iOS : chaque accès renouvelle les missions quand l'horloge
        /// passe 5 h du matin, sans appel explicite.
        [Test]
        public void ReadingRollsOverWhenTheClockPassesFiveAm()
        {
            MissionRepository r = _w.Missions;
            var day1 = r.TodaysMissions.Select(m => m.Definition.Id).ToList();
            Assert.AreEqual(20260910, _w.Store.GetInt(SaveKeys.MissionsDayKey));
            CollectionAssert.AreEqual(MissionRepository.RollFor(20260910).Select(e => e.Id), day1);
            _w.Clock.Advance(System.TimeSpan.FromHours(17));   // 11/09 à 5 h UTC
            r.ReportMove();
            Assert.AreEqual(20260911, _w.Store.GetInt(SaveKeys.MissionsDayKey));
        }

        /// Format iOS : `[{"id","value","claimed"}]`.
        [Test]
        public void ProgressIsStoredAsIosJson()
        {
            _w.Missions.EnsureRolled(20260910);
            string json = _w.Store.GetString(SaveKeys.MissionsProgress);
            StringAssert.StartsWith("[{\"id\":", json);
            StringAssert.Contains("\"value\":0,\"claimed\":false", json);
        }
    }
}
