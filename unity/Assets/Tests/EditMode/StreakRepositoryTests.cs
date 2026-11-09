using System;
using System.Linq;
using NUnit.Framework;
using TenGo.Core;

namespace TenGo.Tests
{
    /// Port de StreakRepositoryTest.kt.
    public class StreakRepositoryTests
    {
        private TestWorld _w;
        private StreakRepository Streak => _w.Streak;
        private EconomyRepository Economy => _w.Economy;

        /// 1er septembre 2026 + n jours, à midi UTC.
        private static DateTimeOffset Day(int n) => new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero).AddDays(n);

        [SetUp]
        public void SetUp() => _w = new TestWorld(Day(0));

        [Test]
        public void ConsecutiveDaysExtendAndSameDayIsIdempotent()
        {
            Assert.IsFalse(Streak.RegisterPlay(Day(0)));
            Assert.AreEqual(1, Streak.Current);
            Assert.IsFalse(Streak.RegisterPlay(Day(0)));
            Assert.AreEqual(1, Streak.Current);
            Streak.RegisterPlay(Day(1));
            Assert.AreEqual(2, Streak.Current);
            Assert.AreEqual(2, Streak.Best);
            Assert.IsTrue(Streak.HasPlayedToday(Day(1)));
            Assert.IsFalse(Streak.HasPlayedToday(Day(2)));
        }

        [Test]
        public void ClockRollbackDoesNothing()
        {
            Streak.RegisterPlay(Day(0));
            Streak.RegisterPlay(Day(1));
            Streak.AddShield(1);
            Streak.RegisterPlay(Day(-3));
            Assert.AreEqual(2, Streak.Current);
            Assert.AreEqual(1, Streak.ShieldCount);
        }

        [Test]
        public void MissedDayConsumesShieldOrResets()
        {
            Streak.RegisterPlay(Day(0));
            Streak.RegisterPlay(Day(1));
            Streak.AddShield(1);
            Assert.IsTrue(Streak.RegisterPlay(Day(3)));
            Assert.AreEqual(3, Streak.Current);
            Assert.AreEqual(0, Streak.ShieldCount);
            Assert.IsFalse(Streak.RegisterPlay(Day(6)));
            Assert.AreEqual(1, Streak.Current);
            Assert.AreEqual(3, Streak.Best);
        }

        [Test]
        public void ShieldsAreCapped()
        {
            Streak.AddShield(5);
            Assert.AreEqual(StreakRepository.MaxShields, Streak.ShieldCount);
        }

        [Test]
        public void MilestonesArePaidOnceWithBoosters()
        {
            var paid = Streak.AwardMilestones(7);
            CollectionAssert.AreEqual(new[] { 3, 7 }, paid.Select(m => m.Day));
            Assert.AreEqual(25, Economy.Balance);
            Assert.AreEqual(1, Economy.BoosterCount(Booster.Hint));
            Assert.IsEmpty(Streak.AwardMilestones(7));
            var next = Streak.AwardMilestones(14);
            CollectionAssert.AreEqual(new[] { 14 }, next.Select(m => m.Day));
            Assert.AreEqual(55, Economy.Balance);
            Assert.AreEqual(1, Economy.BoosterCount(Booster.Shuffle));
        }

        [Test]
        public void MigrationFrom23ScaleGrantsDay21Once()
        {
            _w.Store.SetInt(SaveKeys.StreakRewardedMilestone, 30);
            var paid = Streak.AwardMilestones(25);
            CollectionAssert.AreEqual(new[] { 21 }, paid.Select(m => m.Day));
            Assert.AreEqual(75, Economy.Balance);
            Assert.IsEmpty(Streak.AwardMilestones(25));
            Assert.IsTrue(_w.Store.GetBool(SaveKeys.StreakMilestoneMigrated));
        }

        [Test]
        public void NextMilestone()
        {
            Assert.AreEqual(3, StreakRepository.NextMilestone(0)?.Day);
            Assert.AreEqual(7, StreakRepository.NextMilestone(3)?.Day);
            Assert.IsNull(StreakRepository.NextMilestone(365));
        }

        // MARK: - Ajouts Unity

        /// Format iOS : début du jour local en secondes de référence Swift.
        [Test]
        public void LastPlayedIsStoredLikeIos()
        {
            Streak.RegisterPlay(Day(0));
            double stored = _w.Store.GetDouble(SaveKeys.StreakLastPlayed);
            DateTimeOffset expected = new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero);
            Assert.AreEqual(SwiftDate.ToReferenceSeconds(expected), stored, 1e-6);
        }

        /// Les jours sont ceux du calendrier local : 23 h 30 puis 0 h 30 le
        /// lendemain (Paris) prolongent la série.
        [Test]
        public void DaysFollowTheLocalCalendar()
        {
            TimeZoneInfo paris;
            try { paris = TimeZoneInfo.FindSystemTimeZoneById("Europe/Paris"); }
            catch (TimeZoneNotFoundException) { paris = TimeZoneInfo.FindSystemTimeZoneById("Romance Standard Time"); }
            _w.Clock.Zone = paris;
            Streak.RegisterPlay(new DateTimeOffset(2026, 9, 1, 21, 30, 0, TimeSpan.Zero)); // 23 h 30 à Paris
            Streak.RegisterPlay(new DateTimeOffset(2026, 9, 1, 22, 30, 0, TimeSpan.Zero)); // 0 h 30 le 2
            Assert.AreEqual(2, Streak.Current);
        }
    }
}
