using System.Collections.Generic;
using NUnit.Framework;
using TenGo.Core;
using TenGo.Services;

namespace TenGo.Tests
{
    /// Événements analytics : noms, paramètres et types d'AnalyticsService.swift
    /// et FirstRun.swift (3.3.0), tels que Firebase les reçoit.
    public class AnalyticsCatalogTests
    {
        static LogAnalytics NewLog() => new LogAnalytics { Echo = false };

        [Test]
        public void EngagementEventsMatchSwift()
        {
            Assert.AreEqual("level_end level_name=normal success=1 score=420", AnalyticsCatalog.LevelEnd("normal", 420, true).ToString());
            Assert.AreEqual("level_end level_name=rush success=0 score=0", AnalyticsCatalog.LevelEnd("rush", 0, false).ToString());
            Assert.AreEqual("daily_challenge_played score=88", AnalyticsCatalog.DailyChallengePlayed(88).ToString());
            Assert.AreEqual("sound_toggled enabled=0", AnalyticsCatalog.SoundToggled(false).ToString());
            Assert.AreEqual("tutorial_complete", AnalyticsCatalog.TutorialComplete().ToString());
        }

        [Test]
        public void BoosterAndShopEventsMatchSwift()
        {
            Assert.AreEqual("spend_virtual_currency item_name=hammer virtual_currency_name=coins value=150 quantity=3 source=shop",
                AnalyticsCatalog.BoosterPurchased("hammer", 3, 150, "shop").ToString());
            Assert.AreEqual("earn_virtual_currency virtual_currency_name=coins value=50 item_name=shuffle reason=tutorial_refund",
                AnalyticsCatalog.BoosterRefunded("shuffle", 50).ToString());
            Assert.AreEqual("booster_used item_name=bomb", AnalyticsCatalog.BoosterUsed("bomb").ToString());
            Assert.AreEqual("shop_from_game reason=no_coins", AnalyticsCatalog.ShopOpenedFromGame("no_coins").ToString());
            Assert.AreEqual("noads_offer_view source=game_over ads_seen=4 ads_today=2", AnalyticsCatalog.NoAdsOfferView("game_over", 4, 2).ToString());
            Assert.AreEqual("noads_offer_view source=settings", AnalyticsCatalog.NoAdsOfferView("settings").ToString());
            Assert.AreEqual("noads_offer_decline source=game_over", AnalyticsCatalog.NoAdsOfferDeclined("game_over").ToString());
        }

        [Test]
        public void PurchaseFunnelMatchesSwift()
        {
            Assert.AreEqual("iap_tapped item_id=com.tengo.noads source=shop", AnalyticsCatalog.IapTapped("com.tengo.noads", "shop").ToString());
            Assert.AreEqual("iap_success item_id=com.tengo.coins.tier1 source=transaction_update",
                AnalyticsCatalog.IapSucceeded("com.tengo.coins.tier1", "transaction_update", null).ToString());
            Assert.AreEqual("iap_success item_id=com.tengo.noads source=shop store_env=sandbox",
                AnalyticsCatalog.IapSucceeded("com.tengo.noads", "shop", "sandbox").ToString());
            var failed = AnalyticsCatalog.IapFailed("com.tengo.noads", "shop", new string('x', 150));
            Assert.AreEqual(100, ((string)failed["reason"]).Length, "Firebase tronque à 100 caractères");

            var purchase = AnalyticsCatalog.Purchase("com.tengo.coins.tier2", "1200 pièces", 2.99, "EUR", "T-1");
            Assert.AreEqual("purchase value=2.99 currency=EUR transaction_id=T-1 items=[{item_id:com.tengo.coins.tier2,item_name:1200 pièces}]",
                purchase.ToString());
            Assert.IsInstanceOf<double>(purchase["value"]);
        }

        [Test]
        public void AdImpressionConvertsMicros()
        {
            var e = AnalyticsCatalog.AdImpression("rewarded_interstitial", "ca-app-pub-x/1", 12_345, "USD");
            Assert.AreEqual("ad_impression ad_platform=AdMob ad_source=AdMob ad_format=rewarded_interstitial ad_unit_name=ca-app-pub-x/1 value=0.012345 currency=USD",
                e.ToString());
            Assert.AreEqual("rewarded", AdUnits.RewardedFormat(false));
        }

        [Test]
        public void IntegersAreSentAsLong()
        {
            foreach (var e in new[] { AnalyticsCatalog.LevelEnd("normal", 3, true), AnalyticsCatalog.SeedPlanted(4, true),
                         AnalyticsCatalog.FirstGameStart("normal", 12) })
                foreach (var p in e.Parameters)
                    Assert.That(p.Value is string || p.Value is long, $"{e.Name}.{p.Key} : string ou long seulement");
        }

        [Test]
        public void FirstGameCompleteSetsUserProperty()
        {
            var log = NewLog();
            log.FirstGameComplete("normal", 300, false, 95);
            Assert.AreEqual("first_game_complete mode=normal score=300 success=0 seconds_since_first_open=95", log.Events[0]);
            Assert.AreEqual("yes", log.UserProperties["first_game_done"]);
        }

        [Test]
        public void SoundToggleSendsEventAndProperty()
        {
            var log = NewLog();
            log.SoundToggled(true);
            Assert.AreEqual(1, log.Count("sound_toggled"));
            Assert.AreEqual("on", log.UserProperties["sound_on"]);
            log.SetSoundProperty(false);
            Assert.AreEqual("off", log.UserProperties["sound_on"]);
        }

        [Test]
        public void SeedAndMirrorEventsMatchSwift()
        {
            var log = NewLog();
            log.SeedPlanted(5, false);
            log.FlowerUsed(5, 7);
            log.FlowerPlaced(5);
            log.MirrorOpened(12, 4);
            log.MirrorInsightShown("pace");
            CollectionAssert.AreEqual(new List<string>
            {
                "seed_planted power=5 strengthened=0", "flower_used power=5 length=7", "flower_placed power=5",
                "mirror_opened games=12 repertoire=4", "mirror_insight_shown kind=pace",
            }, log.Events);
        }
    }
}
