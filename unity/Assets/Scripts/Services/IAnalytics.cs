using TenGo.Core;

namespace TenGo.Services
{
    /// Événements analytics du jeu (AnalyticsService.swift). Étend les
    /// événements de Core (`IGameAnalytics`) avec ceux que la partie émet
    /// elle-même. Noms et paramètres identiques à iOS ; le service Firebase
    /// arrive à l'étape 7 (`LogAnalytics` en attendant).
    public interface IAnalytics : IGameAnalytics
    {
        /// `flower_used` (power, length).
        void FlowerUsed(int power, int chainLength);
        /// `flower_placed` (power).
        void FlowerPlaced(int power);
        /// `booster_used` (item_name).
        void BoosterUsed(string id);
        /// `booster_coach_shown` (item_name).
        void BoosterCoachShown(string id);
        /// `rewarded_offer_view` (placement).
        void RewardedOfferView(string placement);
        /// `rewarded_started` (placement).
        void RewardedStarted(string placement);
        /// `rewarded_completed` (placement).
        void RewardedCompleted(string placement);
        /// `noads_offer_view` (source, ads_seen, ads_today).
        void NoAdsOfferView(string source, int adsSeen, int adsToday);
        /// `noads_offer_decline` (source).
        void NoAdsOfferDeclined(string source);
        /// `shop_from_game` (reason).
        void ShopOpenedFromGame(string reason);
        /// `tutorial_complete`.
        void TutorialComplete();
        /// `tutorial_skip` (step) : tutoriel jouable passé avant la fin.
        void TutorialSkip(int step);
        /// `welcome_gift` (via, coins, hints) : cadeau de bienvenue versé.
        void WelcomeGift(string via);
        /// `sound_toggled` (enabled) + propriété `sound_on`.
        void SoundToggled(bool enabled);
        /// `spend_virtual_currency` (item_name, virtual_currency_name, value, quantity, source).
        void BoosterPurchased(string id, int quantity, int cost, string source);
        /// `earn_virtual_currency` (reason = tutorial_refund).
        void BoosterRefunded(string id, int amount);
        /// `shop_guide` (step : shown | opened | purchased | refunded | later | gaveup).
        void ShopGuide(string step);
        /// `shop_opened` (source : tab | game).
        void ShopOpened(string source);
        /// `noads_offer_view` (source) sans compteurs (carte de la boutique).
        void NoAdsOfferView(string source);
        /// `mirror_opened` (games, repertoire).
        void MirrorOpened(int gamesMeasured, int repertoire);

        // MARK: Achats en vraie monnaie (StoreManager.swift)

        /// `iap_tapped` (item_id, source).
        void IapTapped(string productId, string source);
        /// `iap_success` (item_id, source, store_env?).
        void IapSucceeded(string productId, string source, string environment);
        /// `iap_failed` (item_id, source, reason).
        void IapFailed(string productId, string source, string reason);
        /// `iap_cancelled` (item_id, source).
        void IapCancelled(string productId, string source);
        /// `iap_pending` (item_id, source).
        void IapPending(string productId, string source);
        /// `purchase` (value, currency, transaction_id, items).
        void Purchase(string productId, string productName, double value, string currency, string transactionId);

        // MARK: Publicité

        /// `ad_impression` (revenu d'un affichage, `paidEventHandler`).
        void AdImpression(string format, string unitName, long valueMicros, string currency);

        // MARK: Propriétés utilisateur

        /// Propriété `sound_on` (« on » / « off »).
        void SetSoundProperty(bool enabled);
        /// Propriété utilisateur libre (`store_env`, `build_channel`…).
        void SetUserProperty(string name, string value);
    }
}
