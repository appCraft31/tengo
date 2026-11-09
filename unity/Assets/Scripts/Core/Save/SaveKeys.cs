#nullable enable
namespace TenGo.Core
{
    /// Clés de sauvegarde, reprises À L'IDENTIQUE du jeu iOS
    /// (`AppConfig.UserDefaultsKey`, `GameState`, `InterstitialAdManager`,
    /// `FirstRun`) : la reprise des données natives (étape 3) les lit telles quelles.
    public static class SaveKeys
    {
        // MARK: - AppConfig.UserDefaultsKey

        public const string HasSeenTutorial = "hasSeenTutorial";
        public const string HasSeenShopPurchaseGuide = "hasSeenShopPurchaseGuide";
        public const string ShopGuideSnooze = "tenGO_shopGuideSnooze";
        public const string BoosterCoachSeenPrefix = "tenGO_boosterCoachSeen_";
        public const string SoundMuted = "tenGO_soundMuted";
        public const string HapticsEnabled = "tenGO_hapticsEnabled";
        public const string ReducedEffects = "tenGO_reducedEffects";
        public const string DailyLastCompletedDayKey = "tenGO_dailyLastCompleted";
        public const string StreakCurrent = "tenGO_streakCurrent";
        public const string StreakBest = "tenGO_streakBest";
        /// Début du dernier jour joué, en secondes de référence Swift (double).
        public const string StreakLastPlayed = "tenGO_streakLastPlayed";
        public const string NotifGamesPlayed = "tenGO_notifGamesPlayed";
        public const string NotifRequested = "tenGO_notifRequested";
        public const string CoinsBalance = "tenGO_coins";
        public const string StreakRewardedMilestone = "tenGO_streakRewardedMilestone";
        public const string StreakShieldCount = "tenGO_streakShieldCount";
        public const string StreakMilestoneMigrated = "tenGO_streakMilestoneMigrated";
        public const string OwnedThemes = "tenGO_ownedThemes";
        public const string ActiveTheme = "tenGO_activeTheme";
        public const string OwnedBubbleStyles = "tenGO_ownedBubbleStyles";
        public const string ActiveBubbleStyle = "tenGO_activeBubbleStyle";
        public const string OwnedTrails = "tenGO_ownedTrails";
        public const string ActiveTrail = "tenGO_activeTrail";
        public const string BoosterInventoryPrefix = "tenGO_booster_";
        public const string RewardedDayKey = "tenGO_rewardedDay";
        public const string RewardedDayCount = "tenGO_rewardedDayCount";
        public const string NoAdsPurchased = "tenGO_noAdsPurchased";
        public const string NoAdsBonusPending = "tenGO_noAdsBonusPending";
        /// Cadeau de bienvenue du tutoriel jouable versé (4.0, sans équivalent natif).
        public const string WelcomeGiftPaid = "tenGO_welcomeGiftPaid";
        public const string TotalXP = "tenGO_totalXP";
        public const string MissionsDayKey = "tenGO_missionsDayKey";
        /// JSON `[{"id","value","claimed"}]`.
        public const string MissionsProgress = "tenGO_missionsProgress";
        public const string StatsBestChain = "tenGO_statsBestChain";
        public const string StatsPerfectTotal = "tenGO_statsPerfectTotal";
        public const string StatsTotalChains = "tenGO_statsTotalChains";
        public const string StatsTotalRushGames = "tenGO_statsTotalRushGames";
        public const string StatsTotalDaily = "tenGO_statsTotalDaily";
        public const string AchievementsUnlocked = "tenGO_achievementsUnlocked";
        public const string PuzzleStarsPrefix = "tenGO_puzzleStars_";
        public const string DuelHistory = "tenGO_duelHistory";
        /// JSON des parties mesurées (dates en secondes de référence Swift).
        public const string MirrorRecords = "tenGO_mirrorRecords";
        /// JSON `{décomposition: date}` (secondes de référence Swift).
        public const string MirrorPartitions = "tenGO_mirrorPartitions";
        /// JSON `{"plantedAt","chain","bloomAt"}` (secondes de référence Swift).
        public const string SeedGarden = "tenGO_seedGarden";
        public const string SeedFlowerExplained = "tenGO_seedFlowerExplained";

        // MARK: - GameState.swift

        /// JSON `{"cells","score","blocked"}` de la partie en cours.
        public const string SavedGame = "tengo_saved_game";
        public const string HighScores = "tengo_high_scores";
        public const string RushHighScores = "tengo_rush_high_scores";

        // MARK: - InterstitialAdManager.swift

        /// Dernier plein écran montré, en secondes de référence Swift.
        public const string InterstitialLastShownAt = "AdMob.interstitialLastShownAt";
        public const string InterstitialsSinceNoAdsOffer = "AdMob.interstitialsSinceNoAdsOffer";
        public const string InterstitialsTodayCount = "AdMob.interstitialsTodayCount";
        /// Chaîne « AAAAMMJJ ».
        public const string InterstitialsTodayDay = "AdMob.interstitialsTodayDay";

        // MARK: - FirstRun.swift

        public const string FirstRunState = "tenGO_firstRunState";
        /// Première ouverture, en secondes de référence Swift.
        public const string FirstOpenAt = "tenGO_firstOpenAt";
        public const string FirstGameStartLogged = "tenGO_firstGameStartLogged";

        // MARK: - Clés composées

        public static string BoosterInventory(string boosterId) => BoosterInventoryPrefix + boosterId;

        public static string BoosterCoachSeen(string boosterId) => BoosterCoachSeenPrefix + boosterId;

        /// `tenGO_puzzleStars_<monde>_<index>`.
        public static string PuzzleStars(int world, int index) => PuzzleStarsPrefix + world + "_" + index;
    }
}
