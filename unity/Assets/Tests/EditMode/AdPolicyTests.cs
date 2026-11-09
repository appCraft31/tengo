using System;
using NUnit.Framework;
using TenGo.Core;

namespace TenGo.Tests
{
    /// Cadence des pubs (InterstitialAdManager, RewardedAdManager) : ajouts Unity.
    public class AdPolicyTests
    {
        private MemorySaveStore _store;
        private FixedClock _clock;

        [SetUp]
        public void SetUp()
        {
            _store = new MemorySaveStore();
            _clock = new FixedClock(new DateTimeOffset(2026, 11, 9, 12, 0, 0, TimeSpan.Zero));
        }

        [Test]
        public void InterstitialWaitsForSecondGameAndCooldown()
        {
            var policy = new InterstitialPolicy(_store, _clock);
            policy.MarkGameCompleted();
            Assert.IsFalse(policy.ShouldShow(AdTrigger.Home, false, true, false));
            policy.MarkGameCompleted();
            Assert.IsTrue(policy.ShouldShow(AdTrigger.Replay, false, true, false));
            Assert.IsFalse(policy.ShouldShow(AdTrigger.Home, true, true, false), "mod sans pub");
            Assert.IsFalse(policy.ShouldShow(AdTrigger.Home, false, false, false), "pas d'annonce prête");
            policy.MarkPresented();
            _clock.Advance(TimeSpan.FromSeconds(149));
            Assert.IsFalse(policy.ShouldShow(AdTrigger.Home, false, true, false));
            _clock.Advance(TimeSpan.FromSeconds(1));
            Assert.IsTrue(policy.ShouldShow(AdTrigger.Home, false, true, false));
            policy.NoteFullScreenAdShown();
            Assert.IsFalse(policy.ShouldShow(AdTrigger.Home, false, true, false), "vidéo récompensée comprise");
        }

        [Test]
        public void FirstSessionNeedsOneMoreGame()
        {
            var policy = new InterstitialPolicy(_store, _clock);
            policy.MarkGameCompleted();
            policy.MarkGameCompleted();
            Assert.IsFalse(policy.ShouldShow(AdTrigger.Home, false, true, true));
            policy.MarkGameCompleted();
            Assert.IsTrue(policy.ShouldShow(AdTrigger.Home, false, true, true));
        }

        [Test]
        public void GameOverAutoIsOneInFourFromThirdGame()
        {
            var policy = new InterstitialPolicy(_store, _clock);
            for (int i = 0; i < 2; i++) policy.MarkGameCompleted();
            Assert.IsFalse(policy.ShouldShow(AdTrigger.GameOverAuto, false, true, false));
            policy.MarkGameCompleted();
            Assert.IsFalse(policy.ShouldShow(AdTrigger.GameOverAuto, false, true, false));
            Assert.IsFalse(policy.ShouldShow(AdTrigger.GameOverAuto, false, true, false));
            Assert.IsFalse(policy.ShouldShow(AdTrigger.GameOverAuto, false, true, false));
            Assert.IsTrue(policy.ShouldShow(AdTrigger.GameOverAuto, false, true, false));
        }

        [Test]
        public void AdFreeOfferFollowsASeriesOfAds()
        {
            var policy = new InterstitialPolicy(_store, _clock);
            policy.RecordShown();
            Assert.IsFalse(policy.CanOfferAdFree(false, true));
            policy.RecordShown();
            Assert.AreEqual(2, policy.ShownToday);
            Assert.AreEqual("20261109", _store.GetString(SaveKeys.InterstitialsTodayDay));
            Assert.IsTrue(policy.CanOfferAdFree(false, true));
            Assert.IsFalse(policy.CanOfferAdFree(false, false), "prix inconnu");
            policy.MarkAdFreeOfferShown();
            Assert.AreEqual(0, policy.ShownSinceNoAdsOffer);
            policy.RecordShown();
            policy.RecordShown();
            policy.MarkAdFreeOfferShown();
            policy.RecordShown();
            policy.RecordShown();
            Assert.IsFalse(policy.CanOfferAdFree(false, true), "deux offres par session au plus");
            _clock.Advance(TimeSpan.FromDays(1));
            Assert.AreEqual(0, policy.ShownToday);
        }

        [Test]
        public void RewardedMenuCoinsAreCappedPerLocalDay()
        {
            var policy = new RewardedPolicy(_store, _clock);
            Assert.AreEqual(5, policy.RemainingToday);
            for (int i = 0; i < 5; i++)
            {
                Assert.IsTrue(policy.CanOffer(RewardedPlacement.MenuCoins, true));
                policy.RecordView(RewardedPlacement.MenuCoins);
            }
            Assert.IsFalse(policy.CanOffer(RewardedPlacement.MenuCoins, true));
            Assert.IsTrue(policy.CanOffer(RewardedPlacement.Hint, true), "indice : pas de quota journalier");
            policy.RecordView(RewardedPlacement.SecondChance);
            Assert.AreEqual(5, policy.CountToday);
            Assert.AreEqual(20261109, _store.GetInt(SaveKeys.RewardedDayKey));
            _clock.Advance(TimeSpan.FromHours(12));
            Assert.IsTrue(policy.CanWatchToday);
            Assert.AreEqual(10, RewardedPolicy.MenuCoins);
            Assert.AreEqual("second_chance", RewardedPlacement.SecondChance.Id());
        }
    }
}
