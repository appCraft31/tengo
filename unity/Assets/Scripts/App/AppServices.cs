using TenGo.Core;
using TenGo.Platform;
using TenGo.Services;
using UnityEngine;

namespace TenGo.App
{
    /// Dépôts de Core et services, tous autour d'un même `ISaveStore` et d'une
    /// même `IClock` (les singletons `*.shared` d'iOS). Composé une fois par
    /// `AppRoot` ; les tests en composent un sur une `MemorySaveStore`.
    public sealed class AppServices
    {
        /// Services remplaçables (tests, pilote) ; `null` = version simulée.
        public sealed class Overrides
        {
            public IAnalytics Analytics;
            public IInterstitialAds Interstitials;
            public IRewardedAds Rewarded;
            public IDuelBackend Duels;
            public IPlayServices Play;
            public IAdFreeStore AdFreeStore;
            public ICoinStore CoinStore;
            public IConsentFlow Consent;
            public IAdsSdk AdsSdk;
        }

        public readonly ISaveStore Store;
        public readonly IClock Clock;
        public readonly SettingsRepository Settings;
        public readonly GameStateRepository Games;
        public readonly EconomyRepository Economy;
        public readonly LevelRepository Level;
        public readonly StreakRepository Streak;
        public readonly MissionRepository Missions;
        public readonly PlayerStatsRepository Stats;
        public readonly AchievementRepository Achievements;
        public readonly PuzzleCatalog Catalog;
        public readonly PuzzleProgressRepository Puzzles;
        public readonly MirrorRepository Mirror;
        public readonly SeedRepository Seeds;
        public readonly FirstRunState FirstRun;
        public readonly ThemeRepository Themes;
        public readonly CosmeticRepository Cosmetics;
        public readonly AdFreeRepository AdFree;
        public readonly DuelHistoryRepository DuelHistory;

        public readonly IAnalytics Analytics;
        public readonly InterstitialAdManager Interstitials;
        public readonly RewardedAdManager Rewarded;
        public readonly IDuelBackend Duels;
        public readonly IPlayServices Play;
        public readonly IAdFreeStore AdFreeStore;
        public readonly ICoinStore CoinStore;
        public readonly IConsentFlow Consent;
        public readonly IAdsSdk AdsSdk;
        public readonly StoreFlow StoreFlow;
        public readonly GameEndCoordinator Coordinator;

        public AppServices(ISaveStore store, IClock clock, Overrides o = null)
        {
            o ??= new Overrides();
            Store = store;
            Clock = clock;
            Analytics = o.Analytics ?? RealServices.Analytics() ?? new LogAnalytics();
            Settings = new SettingsRepository(store);
            Games = new GameStateRepository(store);
            Economy = new EconomyRepository(store);
            Level = new LevelRepository(store);
            Streak = new StreakRepository(store, Economy, clock);
            Missions = new MissionRepository(store, Economy, Level, Streak, clock, SystemRng.Create());
            Stats = new PlayerStatsRepository(store);
            Achievements = new AchievementRepository(store, Economy, Level, Stats, Games, Streak);
            Catalog = LoadCatalog();
            Puzzles = new PuzzleProgressRepository(store, Catalog);
            Mirror = new MirrorRepository(store, clock);
            Seeds = new SeedRepository(store, clock);
            FirstRun = new FirstRunState(store, clock, Analytics);
            Themes = new ThemeRepository(store, Economy);
            Cosmetics = new CosmeticRepository(store, Economy);
            AdFree = new AdFreeRepository(store, Economy);
            DuelHistory = new DuelHistoryRepository(store);

            // Services réels sur appareil (symboles TENGO_*, voir RealServices),
            // simulés dans l'éditeur et les tests.
            AdsSdk = o.AdsSdk ?? RealServices.AdsSdk() ?? new SimulatedAdsSdk();
            StoreFlow = new StoreFlow(Economy, AdFree, Analytics);
            var interstitialPolicy = new InterstitialPolicy(store, clock);
            Interstitials = new InterstitialAdManager(interstitialPolicy,
                o.Interstitials ?? RealServices.Interstitials(AdsSdk, Analytics, () => AdFree.IsPurchased) ?? new SimulatedInterstitialAds(),
                () => AdFree.IsPurchased, () => FirstRun.IsFirstSession);
            Rewarded = new RewardedAdManager(new RewardedPolicy(store, clock),
                o.Rewarded ?? RealServices.Rewarded(AdsSdk, Analytics) ?? new SimulatedRewardedAds(), interstitialPolicy, Analytics);
            Play = o.Play ?? RealServices.Play(Settings, Streak) ?? new LogPlayServices(Settings);
            Duels = o.Duels ?? RealServices.Duels(DuelHistory, () => Play.PlayerName) ?? new SimulatedDuelBackend(DuelHistory);
            var realStore = RealServices.Store(StoreFlow, AdFree);
            AdFreeStore = o.AdFreeStore ?? realStore as IAdFreeStore ?? new SimulatedAdFreeStore(AdFree);
            CoinStore = o.CoinStore ?? realStore as ICoinStore ?? new SimulatedCoinStore(Economy, AdFree);
            Consent = o.Consent ?? RealServices.Consent(Analytics) ?? new SimulatedConsentFlow();
            Coordinator = new GameEndCoordinator(Games, Economy, Level, Missions, Stats, Achievements, Puzzles, Streak,
                clock, Analytics, Mirror, Seeds, FirstRun);
            Coordinator.SeedPlanted += ms => Play.ScheduleSeedBloom(ms);
        }

        /// Catalogue des puzzles (`Resources/Puzzles/puzzles.json`).
        public static PuzzleCatalog LoadCatalog()
        {
            var asset = Resources.Load<TextAsset>(PuzzleCatalog.ResourcePath);
            return asset != null ? PuzzleCatalog.FromJson(asset.text) : new PuzzleCatalog(new PuzzleLevel[0]);
        }

        /// Table des textes (`Resources/Localization/strings.json`), langue `language`.
        public static void LoadStrings(string language)
        {
            var asset = Resources.Load<TextAsset>("Localization/strings");
            if (asset == null) { Debug.LogError("[App] Resources/Localization/strings.json introuvable"); return; }
            Loc.Load(asset.text, language);
            Rendering.Fonts.ApplyLanguage(Loc.Language);
        }

        /// Applique l'apparence active (thème, matière, tracé) aux vues à venir.
        public void ApplyLook() =>
            Rendering.Look.Apply(Themes.ActiveId, Cosmetics.ActiveId(CosmeticKind.BubbleStyle), Cosmetics.ActiveId(CosmeticKind.Trail));
    }
}
