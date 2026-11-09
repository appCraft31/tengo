#nullable enable
using System.Collections.Generic;

namespace TenGo.Core
{
    /// Table des événements d'AnalyticsService.swift et FirstRun.swift (3.3.0) :
    /// mêmes noms, mêmes paramètres, mêmes types. Les constantes Firebase de
    /// Swift (`AnalyticsEventLevelEnd`, `AnalyticsParameterScore`…) sont écrites
    /// en clair. Swift fait foi là où Kotlin diverge (`level_end` porte
    /// `level_name` et un `success` entier, pas `mode` ni « win »/« lose »).
    public static class AnalyticsCatalog
    {
        // MARK: Propriétés utilisateur

        public const string SoundOnProperty = "sound_on";
        public const string FirstGameDoneProperty = "first_game_done";
        public const string StoreEnvProperty = "store_env";
        /// Canal de la build (development, testflight, production), comme Binero.
        public const string BuildChannelProperty = "build_channel";

        public static string SoundOnValue(bool enabled) => enabled ? "on" : "off";

        // MARK: Engagement

        public static AnalyticsEvent TutorialComplete() => new("tutorial_complete");

        public static AnalyticsEvent LevelEnd(string mode, int score, bool won) =>
            new AnalyticsEvent("level_end").Add("level_name", mode).Add("success", won ? 1 : 0).Add("score", score);

        public static AnalyticsEvent DailyChallengePlayed(int score) =>
            new AnalyticsEvent("daily_challenge_played").Add("score", score);

        public static AnalyticsEvent SoundToggled(bool enabled) =>
            new AnalyticsEvent("sound_toggled").Add("enabled", enabled ? 1 : 0);

        // MARK: Boosters

        public static AnalyticsEvent BoosterPurchased(string id, int quantity, int cost, string source) =>
            new AnalyticsEvent("spend_virtual_currency").Add("item_name", id).Add("virtual_currency_name", "coins")
                .Add("value", cost).Add("quantity", quantity).Add("source", source);

        public static AnalyticsEvent BoosterRefunded(string id, int amount) =>
            new AnalyticsEvent("earn_virtual_currency").Add("virtual_currency_name", "coins").Add("value", amount)
                .Add("item_name", id).Add("reason", "tutorial_refund");

        public static AnalyticsEvent BoosterUsed(string id) => new AnalyticsEvent("booster_used").Add("item_name", id);
        public static AnalyticsEvent BoosterCoachShown(string id) => new AnalyticsEvent("booster_coach_shown").Add("item_name", id);
        public static AnalyticsEvent ShopGuide(string step) => new AnalyticsEvent("shop_guide").Add("step", step);
        public static AnalyticsEvent ShopOpenedFromGame(string reason) => new AnalyticsEvent("shop_from_game").Add("reason", reason);

        // MARK: Entonnoir d'achat

        public static AnalyticsEvent ShopOpened(string source) => new AnalyticsEvent("shop_opened").Add("source", source);

        public static AnalyticsEvent NoAdsOfferView(string source, int? adsSeen = null, int? adsToday = null)
        {
            var e = new AnalyticsEvent("noads_offer_view").Add("source", source);
            if (adsSeen is int seen) e.Add("ads_seen", seen);
            if (adsToday is int today) e.Add("ads_today", today);
            return e;
        }

        public static AnalyticsEvent NoAdsOfferDeclined(string source) => new AnalyticsEvent("noads_offer_decline").Add("source", source);

        public static AnalyticsEvent IapTapped(string productId, string source) => Iap("iap_tapped", productId, source);
        public static AnalyticsEvent IapCancelled(string productId, string source) => Iap("iap_cancelled", productId, source);
        public static AnalyticsEvent IapPending(string productId, string source) => Iap("iap_pending", productId, source);

        /// `source = transaction_update` : achat approuvé hors de la feuille d'achat.
        public static AnalyticsEvent IapSucceeded(string productId, string source, string? environment)
        {
            var e = Iap("iap_success", productId, source);
            if (environment != null) e.Add("store_env", environment);
            return e;
        }

        /// Firebase tronque les valeurs au-delà de 100 caractères.
        public static AnalyticsEvent IapFailed(string productId, string source, string reason) =>
            Iap("iap_failed", productId, source).Add("reason", Truncate(reason, 100));

        static AnalyticsEvent Iap(string name, string productId, string source) =>
            new AnalyticsEvent(name).Add("item_id", productId).Add("source", source);

        /// Achat en vraie monnaie validé (conversion Google Ads).
        public static AnalyticsEvent Purchase(string productId, string productName, double value, string currency, string transactionId) =>
            new AnalyticsEvent("purchase").Add("value", value).Add("currency", currency).Add("transaction_id", transactionId)
                .Add("items", new List<IReadOnlyDictionary<string, object>>
                {
                    new Dictionary<string, object> { ["item_id"] = productId, ["item_name"] = productName },
                });

        /// Impression valorisée (`paidEventHandler`) : valeur en micros convertie en unités.
        public static AnalyticsEvent AdImpression(string format, string unitName, long valueMicros, string currency) =>
            new AnalyticsEvent("ad_impression").Add("ad_platform", "AdMob").Add("ad_source", "AdMob")
                .Add("ad_format", format).Add("ad_unit_name", unitName)
                .Add("value", valueMicros / 1_000_000.0).Add("currency", currency);

        // MARK: Vidéo récompensée

        public static AnalyticsEvent RewardedOfferView(string placement) => new AnalyticsEvent("rewarded_offer_view").Add("placement", placement);
        public static AnalyticsEvent RewardedStarted(string placement) => new AnalyticsEvent("rewarded_started").Add("placement", placement);
        public static AnalyticsEvent RewardedCompleted(string placement) => new AnalyticsEvent("rewarded_completed").Add("placement", placement);

        // MARK: Miroir et graines

        public static AnalyticsEvent MirrorInsightShown(string kind) => new AnalyticsEvent("mirror_insight_shown").Add("kind", kind);
        public static AnalyticsEvent MirrorOpened(int games, int repertoire) =>
            new AnalyticsEvent("mirror_opened").Add("games", games).Add("repertoire", repertoire);
        public static AnalyticsEvent SeedPlanted(int power, bool strengthened) =>
            new AnalyticsEvent("seed_planted").Add("power", power).Add("strengthened", strengthened ? 1 : 0);
        public static AnalyticsEvent FlowerPlaced(int power) => new AnalyticsEvent("flower_placed").Add("power", power);
        public static AnalyticsEvent FlowerUsed(int power, int length) =>
            new AnalyticsEvent("flower_used").Add("power", power).Add("length", length);

        // MARK: Premier lancement (FirstRun.swift)

        public static AnalyticsEvent FirstGameStart(string mode, long secondsSinceFirstOpen) =>
            new AnalyticsEvent("first_game_start").Add("mode", mode).Add("seconds_since_first_open", secondsSinceFirstOpen);

        public static AnalyticsEvent FirstGameComplete(string mode, int score, bool won, long secondsSinceFirstOpen) =>
            new AnalyticsEvent("first_game_complete").Add("mode", mode).Add("score", score).Add("success", won ? 1 : 0)
                .Add("seconds_since_first_open", secondsSinceFirstOpen);

        static string Truncate(string s, int max) => s == null ? "" : s.Length <= max ? s : s.Substring(0, max);
    }
}
