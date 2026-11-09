using System;
using TenGo.Core;

namespace TenGo.Services
{
    /// `InterstitialAdManager.maybeShow(trigger:from:completion:)` : la
    /// politique de Core décide, le réseau (`IInterstitialAds`) affiche.
    /// `completion(adShown)` est toujours appelé une fois.
    public sealed class InterstitialAdManager
    {
        readonly InterstitialPolicy _policy;
        readonly IInterstitialAds _ads;
        readonly Func<bool> _isAdFree;
        readonly Func<bool> _isFirstSession;

        public InterstitialAdManager(InterstitialPolicy policy, IInterstitialAds ads, Func<bool> isAdFree, Func<bool> isFirstSession)
        {
            _policy = policy;
            _ads = ads;
            _isAdFree = isAdFree;
            _isFirstSession = isFirstSession;
        }

        public InterstitialPolicy Policy => _policy;

        /// Fin de partie (gagnée ou perdue) : compte la partie, puis précharge
        /// paresseusement la première annonce (le réseau ignore la demande tant
        /// que le SDK n'a pas démarré, ou si le mod « sans pub » est acheté).
        public void MarkGameCompleted()
        {
            _policy.MarkGameCompleted();
            if (!_ads.IsReady && !_isAdFree()) _ads.Load();
        }

        public void MaybeShow(AdTrigger trigger, Action<bool> completion)
        {
            if (!_policy.ShouldShow(trigger, _isAdFree(), _ads.IsReady, _isFirstSession()))
            {
                completion?.Invoke(false);
                return;
            }
            _ads.Show(_policy.MarkPresented, shown =>
            {
                if (shown) _policy.RecordShown();
                completion?.Invoke(shown);
            });
        }
    }
}
