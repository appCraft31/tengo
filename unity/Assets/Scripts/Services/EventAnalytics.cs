using TenGo.Core;

namespace TenGo.Services
{
    /// Base commune des analytics : chaque appel devient un `AnalyticsEvent`
    /// d'`AnalyticsCatalog` (noms et paramètres de la 3.3.0), que la sous-classe
    /// envoie (`Send`) ou journalise.
    public abstract class EventAnalytics : IAnalytics
    {
        protected abstract void Send(AnalyticsEvent e);
        public abstract void SetUserProperty(string name, string value);

        public void LevelEnd(string mode, int score, bool won) => Send(AnalyticsCatalog.LevelEnd(mode, score, won));
        public void DailyChallengePlayed(int score) => Send(AnalyticsCatalog.DailyChallengePlayed(score));
        public void MirrorInsightShown(string kind) => Send(AnalyticsCatalog.MirrorInsightShown(kind));
        public void SeedPlanted(int power, bool strengthened) => Send(AnalyticsCatalog.SeedPlanted(power, strengthened));
        public void FirstGameStart(string mode, long seconds) => Send(AnalyticsCatalog.FirstGameStart(mode, seconds));

        /// FirstRun.swift : l'événement, puis la propriété `first_game_done = yes`.
        public void FirstGameComplete(string mode, int score, bool won, long seconds)
        {
            Send(AnalyticsCatalog.FirstGameComplete(mode, score, won, seconds));
            SetUserProperty(AnalyticsCatalog.FirstGameDoneProperty, "yes");
        }

        public void FlowerUsed(int power, int chainLength) => Send(AnalyticsCatalog.FlowerUsed(power, chainLength));
        public void FlowerPlaced(int power) => Send(AnalyticsCatalog.FlowerPlaced(power));
        public void BoosterUsed(string id) => Send(AnalyticsCatalog.BoosterUsed(id));
        public void BoosterCoachShown(string id) => Send(AnalyticsCatalog.BoosterCoachShown(id));
        public void RewardedOfferView(string placement) => Send(AnalyticsCatalog.RewardedOfferView(placement));
        public void RewardedStarted(string placement) => Send(AnalyticsCatalog.RewardedStarted(placement));
        public void RewardedCompleted(string placement) => Send(AnalyticsCatalog.RewardedCompleted(placement));
        public void NoAdsOfferView(string source, int adsSeen, int adsToday) => Send(AnalyticsCatalog.NoAdsOfferView(source, adsSeen, adsToday));
        public void NoAdsOfferView(string source) => Send(AnalyticsCatalog.NoAdsOfferView(source));
        public void NoAdsOfferDeclined(string source) => Send(AnalyticsCatalog.NoAdsOfferDeclined(source));
        public void ShopOpenedFromGame(string reason) => Send(AnalyticsCatalog.ShopOpenedFromGame(reason));
        public void TutorialComplete() => Send(AnalyticsCatalog.TutorialComplete());
        public void TutorialSkip(int step) => Send(AnalyticsCatalog.TutorialSkip(step));
        public void WelcomeGift(string via) => Send(AnalyticsCatalog.WelcomeGift(via, Core.WelcomeGift.Coins, Core.WelcomeGift.Hints));

        /// `sound_toggled` puis la propriété `sound_on`.
        public void SoundToggled(bool enabled)
        {
            Send(AnalyticsCatalog.SoundToggled(enabled));
            SetSoundProperty(enabled);
        }

        public void SetSoundProperty(bool enabled) => SetUserProperty(AnalyticsCatalog.SoundOnProperty, AnalyticsCatalog.SoundOnValue(enabled));
        public void BoosterPurchased(string id, int quantity, int cost, string source) => Send(AnalyticsCatalog.BoosterPurchased(id, quantity, cost, source));
        public void BoosterRefunded(string id, int amount) => Send(AnalyticsCatalog.BoosterRefunded(id, amount));
        public void ShopGuide(string step) => Send(AnalyticsCatalog.ShopGuide(step));
        public void ShopOpened(string source) => Send(AnalyticsCatalog.ShopOpened(source));
        public void MirrorOpened(int gamesMeasured, int repertoire) => Send(AnalyticsCatalog.MirrorOpened(gamesMeasured, repertoire));
        public void IapTapped(string productId, string source) => Send(AnalyticsCatalog.IapTapped(productId, source));
        public void IapSucceeded(string productId, string source, string environment) => Send(AnalyticsCatalog.IapSucceeded(productId, source, environment));
        public void IapFailed(string productId, string source, string reason) => Send(AnalyticsCatalog.IapFailed(productId, source, reason));
        public void IapCancelled(string productId, string source) => Send(AnalyticsCatalog.IapCancelled(productId, source));
        public void IapPending(string productId, string source) => Send(AnalyticsCatalog.IapPending(productId, source));
        public void Purchase(string productId, string productName, double value, string currency, string transactionId) =>
            Send(AnalyticsCatalog.Purchase(productId, productName, value, currency, transactionId));
        public void AdImpression(string format, string unitName, long valueMicros, string currency) =>
            Send(AnalyticsCatalog.AdImpression(format, unitName, valueMicros, currency));
    }
}
