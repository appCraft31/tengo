namespace TenGo.Services
{
    /// Identifiants AdMob (InterstitialAdManager.swift, RewardedAdManager.swift,
    /// AppConfig.kt). Builds de développement : toujours les blocs de test
    /// publics de Google, jamais d'annonce réelle sur nos appareils (risque de
    /// suspension du compte AdMob).
    ///
    /// La vidéo récompensée est au format « avec récompense » sur les deux
    /// plateformes. Jusqu'à la 4.0.0, iOS utilisait un interstitiel récompensé
    /// (bloc 2440159179), format que Unity Ads ne sert pas : le bloc iOS
    /// « recompense v2 » a été créé le 2026-11-20 pour ouvrir la médiation
    /// Unity Ads (enchères) à la vidéo récompensée.
    ///
    /// Médiation (console AdMob, enchères) : Liftoff Monetize et Unity Ads.
    /// Unity Ads — Game ID iOS 800104462 (placements BP_Interstitial_iOS,
    /// BP_Rewarded_iOS), Android 800368789 (BP_Interstitial_Android,
    /// BP_Rewarded_Android). Rien de tout cela n'apparaît dans le code : les
    /// adaptateurs (paquets com.google.ads.mobile.mediation.*) suffisent.
    public static class AdUnits
    {
        public const string IosAppId = "ca-app-pub-4352408747876735~9718003723";
        public const string AndroidAppId = "ca-app-pub-4352408747876735~2101345888";

        public const string IosInterstitial = "ca-app-pub-4352408747876735/5201754193";
        public const string IosRewarded = "ca-app-pub-4352408747876735/7989844573";
        public const string IosTestInterstitial = "ca-app-pub-3940256099942544/4411468910";
        public const string IosTestRewarded = "ca-app-pub-3940256099942544/1712485313";

        public const string AndroidInterstitial = "ca-app-pub-4352408747876735/3669994680";
        public const string AndroidRewarded = "ca-app-pub-4352408747876735/7026785763";
        public const string AndroidTestInterstitial = "ca-app-pub-3940256099942544/1033173712";
        public const string AndroidTestRewarded = "ca-app-pub-3940256099942544/5224354917";

        /// Format de la vidéo récompensée dans `ad_impression`.
        public const string RewardedFormat = "rewarded";

        public static string Interstitial(bool ios, bool test) =>
            ios ? (test ? IosTestInterstitial : IosInterstitial) : (test ? AndroidTestInterstitial : AndroidInterstitial);

        public static string Rewarded(bool ios, bool test) =>
            ios ? (test ? IosTestRewarded : IosRewarded) : (test ? AndroidTestRewarded : AndroidRewarded);
    }
}
