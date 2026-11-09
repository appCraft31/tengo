using System;
using NUnit.Framework;
using TenGo.Core;

namespace TenGo.Tests
{
    /// Calendrier des notifications locales (NotificationManager.swift).
    public class NotificationPlanTests
    {
        static DateTime At(int day, int hour, int minute = 0) => new(2026, 11, day, hour, minute, 0, DateTimeKind.Local);

        [Test]
        public void IdentifiersMatchIos()
        {
            Assert.AreEqual("tenGO.dailyAvailable", NotificationPlan.DailyAvailableId);
            Assert.AreEqual("tenGO.streakAtRisk", NotificationPlan.StreakAtRiskId);
            Assert.AreEqual("tenGO.seedBloom", NotificationPlan.SeedBloomId);
            Assert.AreEqual("tenGO.dailyReminder", NotificationPlan.LegacyReminderId);
        }

        [Test]
        public void PermissionAskedOnceFromTheThirdGame()
        {
            Assert.IsFalse(NotificationPlan.ShouldRequest(2, false));
            Assert.IsTrue(NotificationPlan.ShouldRequest(3, false));
            Assert.IsTrue(NotificationPlan.ShouldRequest(7, false));
            Assert.IsFalse(NotificationPlan.ShouldRequest(3, true));
        }

        [Test]
        public void DailyAvailableAtNine()
        {
            Assert.AreEqual(At(9, 9), NotificationPlan.NextDailyAvailable(At(9, 7)));
            Assert.AreEqual(At(10, 9), NotificationPlan.NextDailyAvailable(At(9, 9)));
            Assert.AreEqual(At(10, 9), NotificationPlan.NextDailyAvailable(At(9, 22)));
        }

        [Test]
        public void StreakAtRiskTargetsTheNextEveningAtStake()
        {
            Assert.IsNull(NotificationPlan.StreakAtRisk(0, false, At(9, 10)), "pas de série, pas de rappel");
            Assert.AreEqual(At(9, 20), NotificationPlan.StreakAtRisk(4, false, At(9, 10)), "ce soir");
            Assert.AreEqual(At(10, 20), NotificationPlan.StreakAtRisk(4, true, At(9, 10)), "déjà jouée : demain");
            Assert.AreEqual(At(10, 20), NotificationPlan.StreakAtRisk(4, false, At(9, 21)), "20 h passé : demain");
        }

        [Test]
        public void SeedBloomAnnouncedInsideTheDayWindow()
        {
            Assert.AreEqual(At(9, 14, 37), NotificationPlan.SeedBloom(At(9, 14, 37).AddSeconds(42)), "à la minute");
            Assert.AreEqual(At(9, 21, 30), NotificationPlan.SeedBloom(At(9, 21, 30)), "21 h incluse");
            Assert.AreEqual(At(9, 9), NotificationPlan.SeedBloom(At(9, 3, 15)), "nuit : le matin même");
            Assert.AreEqual(At(10, 9), NotificationPlan.SeedBloom(At(9, 23, 5)), "soir : le lendemain");
        }
    }
}
