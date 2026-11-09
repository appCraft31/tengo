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
    }
}
