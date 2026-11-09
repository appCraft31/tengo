using System;
using TenGo.Core;

namespace TenGo.Services
{
    /// `RewardedAdManager` : politique de Core + réseau simulé ou réel. Toute
    /// vidéo vue relance le délai des interstitielles (`noteFullScreenAdShown`).
    public sealed class RewardedAdManager
    {
        readonly RewardedPolicy _policy;
        readonly IRewardedAds _ads;
        readonly InterstitialPolicy _interstitials;
        readonly IAnalytics _analytics;

        public RewardedAdManager(RewardedPolicy policy, IRewardedAds ads, InterstitialPolicy interstitials, IAnalytics analytics)
        {
            _policy = policy;
            _ads = ads;
            _interstitials = interstitials;
            _analytics = analytics;
        }

        public RewardedPolicy Policy => _policy;

        public void PreloadIfNeeded() => _ads.Preload();

        public bool CanOffer(RewardedPlacement placement) => _policy.CanOffer(placement, _ads.IsReady);

        public void Show(RewardedPlacement placement, Action onReward, Action<bool> onDismiss, Action onUnavailable)
        {
            if (!_ads.IsReady) { onUnavailable?.Invoke(); return; }
            _analytics?.RewardedStarted(placement.Id());
            _ads.Show(placement, () =>
            {
                _policy.RecordView(placement);
                _analytics?.RewardedCompleted(placement.Id());
                onReward?.Invoke();
            }, rewarded =>
            {
                _interstitials.NoteFullScreenAdShown();
                onDismiss?.Invoke(rewarded);
            }, onUnavailable);
        }
    }
}
