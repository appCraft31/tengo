#if TENGO_ADMOB
using System;
using System.Collections.Generic;
using GoogleMobileAds.Api;
using UnityEngine;

namespace TenGo.Services
{
    /// SDK Google Mobile Ads (symbole TENGO_ADMOB). Démarré une fois le
    /// consentement recueilli (`AppRoot.GatherConsent`) ; hors mode personnalisé,
    /// `publisherPrivacyPersonalizationState = disabled` pour toute la session
    /// et `npa=1` sur chaque requête (`Request.consentAware()` de ConsentManager.swift).
    public sealed class AdMobSdk : IAdsSdk
    {
        public static bool UseTestAds => Debug.isDebugBuild;
#if UNITY_IOS
        public const bool IsIos = true;
#else
        public const bool IsIos = false;
#endif

        public bool Started { get; private set; }
        public bool Personalized { get; private set; } = true;
        bool _starting;
        readonly List<Action> _waiting = new();

        public void Start(bool personalized, Action onReady)
        {
            if (Started) { onReady?.Invoke(); return; }
            if (onReady != null) _waiting.Add(onReady);
            if (_starting) return;
            _starting = true;
            Personalized = personalized;
            // Les rappels du SDK arrivent sur ses fils : chacun est ramené sur
            // celui d'Unity par `MainThread.Run`.
            if (!personalized)
            {
                var config = MobileAds.GetRequestConfiguration() ?? new RequestConfiguration();
                config.PublisherPrivacyPersonalizationState = PublisherPrivacyPersonalizationState.Disabled;
                MobileAds.SetRequestConfiguration(config);
            }
            Debug.Log("[AdMob] Démarrage pubs — mode " + (personalized ? "personnalisé" : "non personnalisé"));
            MobileAds.Initialize(_ => MainThread.Run(() =>
            {
                Started = true;
                var waiting = _waiting.ToArray();
                _waiting.Clear();
                foreach (var a in waiting) a();
            }));
        }

        /// Requête qui respecte le consentement (npa=1 hors mode personnalisé).
        public AdRequest NewRequest()
        {
            var request = new AdRequest();
            if (!Personalized) request.Extras["npa"] = "1";
            return request;
        }
    }

    /// Interstitielle AdMob (InterstitialAdManager.swift, partie réseau) :
    /// chargement paresseux, nouvel essai espacé après un échec (30 s doublés
    /// jusqu'à 10 min), annonce jetée au bout de 55 min. La cadence reste dans
    /// `InterstitialPolicy` (Core).
    public sealed class AdMobInterstitialAds : IInterstitialAds
    {
        const double MaxRetryDelay = 600;
        static readonly TimeSpan CacheLifetime = TimeSpan.FromMinutes(55);

        readonly AdMobSdk _sdk;
        readonly IAnalytics _analytics;
        readonly Func<bool> _isAdFree;
        InterstitialAd _ad;
        bool _loading;
        DateTime _loadedAt;
        int _failedLoads;
        DateTime _nextLoadAllowedAt = DateTime.MinValue;

        public AdMobInterstitialAds(AdMobSdk sdk, IAnalytics analytics, Func<bool> isAdFree)
        {
            _sdk = sdk;
            _analytics = analytics;
            _isAdFree = isAdFree;
        }

        string UnitId => AdUnits.Interstitial(AdMobSdk.IsIos, AdMobSdk.UseTestAds);

        public bool IsReady
        {
            get
            {
                DiscardIfExpired();
                return _ad != null && _ad.CanShowAd();
            }
        }

        public void Load()
        {
            if (!_sdk.Started || _isAdFree() || _loading || _ad != null || DateTime.UtcNow < _nextLoadAllowedAt) return;
            string unit = UnitId;
            _loading = true;
            InterstitialAd.Load(unit, _sdk.NewRequest(), (ad, error) => MainThread.Run(() =>
            {
                _loading = false;
                if (error != null || ad == null)
                {
                    _failedLoads++;
                    double delay = Math.Min(30 * Math.Pow(2, _failedLoads - 1), MaxRetryDelay);
                    _nextLoadAllowedAt = DateTime.UtcNow.AddSeconds(delay);
                    Debug.Log($"[AdMob] Interstitiel échec chargement ({_failedLoads}), prochain essai dans {(int)delay} s : {error?.GetMessage()}");
                    return;
                }
                _failedLoads = 0;
                _nextLoadAllowedAt = DateTime.MinValue;
                _loadedAt = DateTime.UtcNow;
                ad.OnAdPaid += v => MainThread.Run(() => _analytics?.AdImpression("interstitial", unit, v.Value, v.CurrencyCode));
                _ad = ad;
                Debug.Log("[AdMob] Interstitiel prêt");
            }));
        }

        void DiscardIfExpired()
        {
            if (_ad == null || DateTime.UtcNow - _loadedAt < CacheLifetime) return;
            _ad.Destroy();
            _ad = null;
        }

        public void Show(Action onPresented, Action<bool> onClosed)
        {
            if (!IsReady) { onClosed?.Invoke(false); return; }
            var ad = _ad;
            _ad = null;
            bool finished = false;
            void Finish(bool shown)
            {
                if (finished) return;
                finished = true;
                ad.Destroy();
                onClosed?.Invoke(shown);
                Load();
            }
            ad.OnAdFullScreenContentClosed += () => MainThread.Run(() => Finish(true));
            ad.OnAdFullScreenContentFailed += e => MainThread.Run(() =>
            {
                Debug.Log("[AdMob] Interstitiel échec affichage : " + e?.GetMessage());
                Finish(false);
            });
            onPresented?.Invoke();
            ad.Show();
        }
    }

    /// Vidéo récompensée AdMob (RewardedAdManager.swift, partie réseau) :
    /// interstitiel récompensé sur iOS, récompensée sur Android. La récompense
    /// est versée à la FERMETURE, pour que l'effet se joue sous les yeux du joueur.
    public sealed class AdMobRewardedAds : IRewardedAds
    {
        readonly AdMobSdk _sdk;
        readonly IAnalytics _analytics;
#if UNITY_IOS
        RewardedInterstitialAd _ad;
#else
        RewardedAd _ad;
#endif
        bool _loading;

        public AdMobRewardedAds(AdMobSdk sdk, IAnalytics analytics)
        {
            _sdk = sdk;
            _analytics = analytics;
        }

        public bool IsReady => _ad != null && _ad.CanShowAd();

        public void Preload()
        {
            if (!_sdk.Started || _loading || _ad != null) return;
            string unit = AdUnits.Rewarded(AdMobSdk.IsIos, AdMobSdk.UseTestAds);
            string format = AdUnits.RewardedFormat(AdMobSdk.IsIos);
            _loading = true;
#if UNITY_IOS
            RewardedInterstitialAd.Load(unit, _sdk.NewRequest(), (ad, error) => MainThread.Run(() =>
#else
            RewardedAd.Load(unit, _sdk.NewRequest(), (ad, error) => MainThread.Run(() =>
#endif
            {
                _loading = false;
                if (error != null || ad == null)
                {
                    Debug.Log("[AdMob] Récompensée échec chargement : " + error?.GetMessage());
                    return;
                }
                ad.OnAdPaid += v => MainThread.Run(() => _analytics?.AdImpression(format, unit, v.Value, v.CurrencyCode));
                _ad = ad;
                Debug.Log("[AdMob] Pub récompensée prête");
            }));
        }

        public void Show(Core.RewardedPlacement placement, Action onReward, Action<bool> onDismiss, Action onUnavailable)
        {
            if (!IsReady)
            {
                onUnavailable?.Invoke();
                Preload();
                return;
            }
            var ad = _ad;
            _ad = null;
            bool earned = false, finished = false;
            void Finish()
            {
                if (finished) return;
                finished = true;
                ad.Destroy();
                if (earned) onReward?.Invoke();
                onDismiss?.Invoke(earned);
                Preload();
            }
            ad.OnAdFullScreenContentClosed += () => MainThread.Run(Finish);
            ad.OnAdFullScreenContentFailed += e => MainThread.Run(() =>
            {
                Debug.Log("[AdMob] Récompensée échec affichage : " + e?.GetMessage());
                Finish();
            });
            ad.Show(_ => MainThread.Run(() => earned = true));
        }
    }
}
#endif
