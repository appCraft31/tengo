namespace TenGo.Services
{
    /// Identifiants AdMob de la 3.3.0 (InterstitialAdManager.swift,
    /// RewardedAdManager.swift, AppConfig.kt). Builds de développement : toujours
    /// les blocs de test publics de Google, jamais d'annonce réelle sur nos
    /// appareils (risque de suspension du compte AdMob).
    ///
    /// La vidéo récompensée n'a pas le même format selon la plateforme, comme
    /// dans les apps natives : interstitiel récompensé sur iOS, récompensée
    /// simple sur Android (les blocs AdMob ont été créés ainsi).
    public static class AdUnits
    {
        public const string IosAppId = "ca-app-pub-4352408747876735~9718003723";
        public const string AndroidAppId = "ca-app-pub-4352408747876735~2101345888";

        public const string IosInterstitial = "ca-app-pub-4352408747876735/5201754193";
        public const string IosRewardedInterstitial = "ca-app-pub-4352408747876735/2440159179";
        public const string IosTestInterstitial = "ca-app-pub-3940256099942544/4411468910";
        public const string IosTestRewardedInterstitial = "ca-app-pub-3940256099942544/6978759866";

        public const string AndroidInterstitial = "ca-app-pub-4352408747876735/3669994680";
        public const string AndroidRewarded = "ca-app-pub-4352408747876735/7026785763";
        public const string AndroidTestInterstitial = "ca-app-pub-3940256099942544/1033173712";
        public const string AndroidTestRewarded = "ca-app-pub-3940256099942544/5224354917";

        /// Format de la vidéo récompensée dans `ad_impression` (iOS : rewarded_interstitial, Android : rewarded).
        public static string RewardedFormat(bool ios) => ios ? "rewarded_interstitial" : "rewarded";

        public static string Interstitial(bool ios, bool test) =>
            ios ? (test ? IosTestInterstitial : IosInterstitial) : (test ? AndroidTestInterstitial : AndroidInterstitial);

        public static string Rewarded(bool ios, bool test) =>
            ios ? (test ? IosTestRewardedInterstitial : IosRewardedInterstitial) : (test ? AndroidTestRewarded : AndroidRewarded);
    }
}
