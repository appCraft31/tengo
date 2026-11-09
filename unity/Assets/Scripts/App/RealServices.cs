using System;
using TenGo.Core;
using TenGo.Services;

namespace TenGo.App
{
    /// Choix des services réels. Chaque SDK n'est utilisé que si son symbole
    /// est défini (Player Settings → Scripting Define Symbols, posés pour
    /// Android et iOS par `ProjectSetup.ApplyDefines`) ET hors éditeur : dans
    /// l'éditeur, les tests et le pilote automatique, tout reste simulé.
    ///
    /// | Symbole | Services |
    /// |---|---|
    /// | `TENGO_ADMOB` | AdMob (interstitielles, récompensées), UMP (Android), ATT (iOS) |
    /// | `TENGO_FIREBASE` | Firebase Analytics, Auth anonyme, Firestore (duels) |
    /// | `TENGO_IAP` | Unity IAP (pièces, mod « sans pub », restauration) |
    ///
    /// Classements, avis, notifications, partage : toujours réels sur appareil.
    /// Chaque fabrique rend `null` quand le service réel n'est pas disponible.
    public static class RealServices
    {
#if !UNITY_EDITOR && (UNITY_IOS || UNITY_ANDROID)
        public const bool OnDevice = true;
#else
        public const bool OnDevice = false;
#endif

        public static IAnalytics Analytics()
        {
#if TENGO_FIREBASE && !UNITY_EDITOR
            return new FirebaseAnalyticsService();
#else
            return null;
#endif
        }

        public static IAdsSdk AdsSdk()
        {
#if TENGO_ADMOB && !UNITY_EDITOR
            return new AdMobSdk();
#else
            return null;
#endif
        }

        public static IInterstitialAds Interstitials(IAdsSdk sdk, IAnalytics analytics, Func<bool> isAdFree)
        {
#if TENGO_ADMOB && !UNITY_EDITOR
            return sdk is AdMobSdk admob ? new AdMobInterstitialAds(admob, analytics, isAdFree) : null;
#else
            return null;
#endif
        }

        public static IRewardedAds Rewarded(IAdsSdk sdk, IAnalytics analytics)
        {
#if TENGO_ADMOB && !UNITY_EDITOR
            return sdk is AdMobSdk admob ? new AdMobRewardedAds(admob, analytics) : null;
#else
            return null;
#endif
        }

        /// Consentement : le résultat ATT aligne le consent mode de Firebase (iOS).
        public static IConsentFlow Consent(IAnalytics analytics)
        {
#if TENGO_ADMOB && !UNITY_EDITOR
            return new NativeConsentFlow(personalized =>
            {
#if TENGO_FIREBASE
                (analytics as FirebaseAnalyticsService)?.ApplyConsent(personalized);
#endif
            });
#else
            return null;
#endif
        }

        public static IDuelBackend Duels(DuelHistoryRepository history, Func<string> playerName)
        {
#if TENGO_FIREBASE && !UNITY_EDITOR
            return new FirestoreDuelBackend(history, playerName);
#else
            return null;
#endif
        }

        /// Magasin réel : à la fois `ICoinStore` et `IAdFreeStore`.
        public static object Store(StoreFlow flow, AdFreeRepository adFree)
        {
#if TENGO_IAP && !UNITY_EDITOR
            return new UnityIapStore(flow, adFree);
#else
            return null;
#endif
        }

        public static IPlayServices Play(SettingsRepository settings, StreakRepository streak)
        {
#if !UNITY_EDITOR && (UNITY_IOS || UNITY_ANDROID)
            return new NativePlayServices(settings, streak);
#else
            return null;
#endif
        }
    }
}
