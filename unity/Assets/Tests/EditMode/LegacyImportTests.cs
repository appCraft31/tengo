using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using NUnit.Framework;
using TenGo.Core;

namespace TenGo.Tests
{
    /// Reprise des données natives : conversion Android → formats iOS, import
    /// iOS, puis relecture par les dépôts de Core ; une seule fois, sans écraser
    /// ni toucher aux données natives.
    public class LegacyImportTests
    {
        /// 9 novembre 2026, 15 h à UTC+1 (clé du jour 20261109).
        private static readonly DateTimeOffset Now = new DateTimeOffset(2026, 11, 9, 15, 0, 0, TimeSpan.FromHours(1));

        private static TestWorld World()
        {
            var world = new TestWorld(Now);
            world.Clock.Zone = LegacyFixtures.Paris;
            return world;
        }

        private static LegacyMigrationReport Migrate(TestWorld world, ILegacySource source) =>
            LegacyMigration.Run(world.Store, source, "4.0.0");

        // MARK: - Android

        /// Toutes les clés écrites par l'app Android 3.3.0 (Prefs.kt,
        /// MirrorRepository.kt, FirstRun.kt, AdsManager.kt) ont une règle.
        [Test]
        public void EveryKotlinKeyHasARule()
        {
            string[] kotlinKeys =
            {
                "tengo_saved_game", "tengo_high_scores", "tenGO_soundMuted", "tenGO_hapticsEnabled", "tenGO_reducedEffects",
                "hasSeenTutorial", "hasSeenShopPurchaseGuide", "tenGO_shopGuideSnooze", "tenGO_boosterCoachSeen_*", "tenGO_coins",
                "tenGO_dailyLastCompleted", "tenGO_noAdsPurchased", "tenGO_noAdsBonusGranted", "tenGO_streakCurrent", "tenGO_streakBest",
                "tenGO_streakLastPlayed", "tenGO_streakRewardedMilestone", "tenGO_streakShieldCount", "tenGO_streakMilestoneMigrated",
                "tenGO_totalXP", "tenGO_missionsDayKey", "tenGO_missionsProgress", "tenGO_statsBestChain", "tenGO_statsPerfectTotal",
                "tenGO_statsTotalChains", "tenGO_statsTotalRushGames", "tenGO_statsTotalDaily", "tenGO_achievementsUnlocked",
                "tenGO_puzzleStars_*", "tenGO_duelHistory", "tengo_rush_high_scores", "tenGO_notifGamesPlayed", "tenGO_notifRequested",
                "tenGO_rewardedDayKey", "tenGO_rewardedCount", "tenGO_ownedThemes", "tenGO_activeTheme", "tenGO_ownedBubbleStyles",
                "tenGO_activeBubbleStyle", "tenGO_ownedTrails", "tenGO_activeTrail", "tenGO_booster_*",
                "tenGO_mirrorData", "tenGO_seedGarden", "tenGO_seedFlowerExplained",
                "tenGO_firstRunState", "tenGO_firstOpenAt", "tenGO_firstGameStartLogged",
                "AdMob.interstitialLastShownAt", "AdMob.interstitialsSinceNoAdsOffer", "AdMob.interstitialsTodayCount", "AdMob.interstitialsTodayDay",
            };
            var ruled = AndroidLegacyImporter.Rules.Select(r => r.key).ToList();
            CollectionAssert.AreEquivalent(kotlinKeys, ruled);
        }

        [Test]
        public void CompleteAndroidDataIsReadBackByCoreRepositories()
        {
            TestWorld w = World();
            LegacyMigrationReport report = Migrate(w, LegacyFixtures.AndroidSource("complete", LegacyFixtures.Paris));
            Assert.AreEqual(LegacyMigrationStatus.Imported, report.Status, report.ToString());
            Assert.AreEqual("android", report.Source);
            Assert.IsEmpty(report.Kept);

            // Économie
            Assert.AreEqual(1234, w.Economy.Balance);
            Assert.AreEqual(4, w.Economy.BoosterCount(Booster.Hint));
            Assert.AreEqual(1, w.Economy.BoosterCount(Booster.Hammer));
            Assert.AreEqual(0, w.Economy.BoosterCount(Booster.Shuffle));
            Assert.AreEqual(2, w.Economy.ShopGuideSnooze);
            Assert.IsFalse(w.Economy.ShopGuideCompleted);
            Assert.IsTrue(w.Economy.IsDailyCompleted(20261108));
            Assert.IsTrue(w.Store.GetBool(SaveKeys.BoosterCoachSeen("hint")));
            var adFree = new AdFreeRepository(w.Store, w.Economy);
            Assert.IsTrue(adFree.IsPurchased);
            Assert.IsFalse(w.Store.Has(SaveKeys.NoAdsBonusPending));
            var themes = new ThemeRepository(w.Store, w.Economy);
            Assert.IsTrue(themes.Owns("night") && themes.Owns("forest") && !themes.Owns("ocean"));
            Assert.AreEqual("night", themes.ActiveId);
            var cosmetics = new CosmeticRepository(w.Store, w.Economy);
            Assert.IsTrue(cosmetics.Owns(CosmeticKind.BubbleStyle, "glass"));
            Assert.AreEqual("glass", cosmetics.ActiveId(CosmeticKind.BubbleStyle));
            Assert.IsTrue(cosmetics.Owns(CosmeticKind.Trail, "neon"));
            Assert.AreEqual("ink", cosmetics.ActiveId(CosmeticKind.Trail));

            // Progression
            Assert.AreEqual(15000, w.Level.TotalXP);
            Assert.AreEqual(9, w.Streak.Current);
            Assert.AreEqual(21, w.Streak.Best);
            Assert.AreEqual(2, w.Streak.ShieldCount);
            Assert.AreEqual(7, w.Store.GetInt(SaveKeys.StreakRewardedMilestone));
            Assert.IsTrue(w.Store.GetBool(SaveKeys.StreakMilestoneMigrated));
            // Jour epoch 20765 = 8 novembre 2026 → minuit local (UTC+1) en secondes Swift.
            Assert.AreEqual(SwiftDate.ToReferenceSeconds(new DateTimeOffset(2026, 11, 8, 0, 0, 0, TimeSpan.FromHours(1))), w.Store.GetDouble(SaveKeys.StreakLastPlayed));
            Assert.IsFalse(w.Streak.HasPlayedToday());
            Assert.IsTrue(w.Streak.HasPlayedToday(new DateTimeOffset(2026, 11, 8, 23, 30, 0, TimeSpan.FromHours(1))));
            Assert.AreEqual(11, w.Stats.BestChainEver);
            Assert.AreEqual(812, w.Stats.TotalChainsMade);
            Assert.AreEqual(14, w.Stats.TotalRushGames);
            Assert.AreEqual(12, w.Stats.TotalDailyCompletions);
            CollectionAssert.AreEquivalent(new[] { "chain5", "perfect1", "streak7" }, w.Achievements.UnlockedIds);
            Assert.AreEqual(3, w.Puzzles.Stars(1, 1));
            Assert.AreEqual(1, w.Puzzles.Stars(1, 12));
            DisplayMission chain = w.Missions.TodaysMissions.Single(m => m.Definition.Id == "chain5x2");
            Assert.AreEqual(2, chain.Progress);
            Assert.IsTrue(chain.Claimed);
            CollectionAssert.AreEqual(new[] { "K7Q2MX", "A1B2C3", "Z9Y8W7" }, new DuelHistoryRepository(w.Store).Codes);

            // Parties
            CollectionAssert.AreEqual(new[] { 5210, 4180, 3640 }, w.Games.HighScores);
            CollectionAssert.AreEqual(new[] { 2360, 1980 }, w.Games.RushHighScores);
            SavedGame save = w.Games.Load();
            Assert.IsNotNull(save);
            Assert.AreEqual(20, save.Score);
            StringAssert.DoesNotContain("\"mode\"", w.Store.GetString(SaveKeys.SavedGame));
            StringAssert.DoesNotContain("\"id\"", w.Store.GetString(SaveKeys.SavedGame));

            // Miroir et graine
            var mirror = new MirrorRepository(w.Store, w.Clock);
            Assert.AreEqual(1, mirror.GamesMeasured);
            Assert.AreEqual(1794000000000L, mirror.Data.Records[0].DateMs);
            Assert.AreEqual(44.5, mirror.Data.Records[0].LookSum);
            Assert.AreEqual(2, mirror.RepertoireCount);
            Assert.AreEqual(1793000000000L, mirror.Data.Discovered["1+9"]);
            var seeds = new SeedRepository(w.Store, w.Clock);
            Assert.AreEqual(new Seed(1794200000000L, 7, 1794214400000L), seeds.Current);
            Assert.IsTrue(seeds.FlowerExplained);

            // Réglages, premier lancement, pubs
            var settings = new SettingsRepository(w.Store);
            Assert.IsTrue(settings.SoundMuted);
            Assert.IsFalse(settings.HapticsEnabled);
            Assert.IsTrue(settings.ReducedEffects);
            Assert.IsTrue(settings.HasSeenTutorial);
            Assert.AreEqual(42, settings.NotifGamesPlayed);
            Assert.IsTrue(settings.NotifRequested);
            var firstRun = new FirstRunState(w.Store, w.Clock, null);
            Assert.IsTrue(firstRun.IsPending);
            Assert.AreEqual(SwiftDate.UnixMsToReferenceSeconds(1794227761614L), w.Store.GetDouble(SaveKeys.FirstOpenAt), 1e-6);
            Assert.IsTrue(w.Store.GetBool(SaveKeys.FirstGameStartLogged));
            Assert.AreEqual(2, new RewardedPolicy(w.Store, w.Clock).RemainingToday);
            var interstitials = new InterstitialPolicy(w.Store, w.Clock);
            Assert.AreEqual(1794227962053L, interstitials.LastShownAt.ToUnixTimeMilliseconds());
            Assert.AreEqual(5, interstitials.ShownSinceNoAdsOffer);
            Assert.AreEqual("20261109", w.Store.GetString(SaveKeys.InterstitialsTodayDay));
            Assert.AreEqual(3, w.Store.GetInt(SaveKeys.InterstitialsTodayCount));

            // Clés laissées de côté, avec leur raison
            StringAssert.Contains("bonus déjà versé", report.Ignored["tenGO_noAdsBonusGranted"]);
            Assert.AreEqual("clé inconnue", report.Ignored["zz_bytes"]);
            Assert.AreEqual("clé inconnue", report.Ignored["tengo_ads.xml:zz_entities"]);
            Assert.IsFalse(w.Store.Values.Keys.Any(k => k.StartsWith("zz_") || k.StartsWith("tenGO_rewardedCount") || k == "tenGO_mirrorData"));
        }

        [Test]
        public void RealEmulatorDataIsReadBackByCoreRepositories()
        {
            TestWorld w = World();
            LegacyMigrationReport report = Migrate(w, LegacyFixtures.AndroidSource("emulator", LegacyFixtures.Paris));
            Assert.AreEqual(LegacyMigrationStatus.Imported, report.Status, report.ToString());
            CollectionAssert.AreEqual(new[] { 4180, 3640, 3120, 2870, 2440, 2050, 1890, 1620, 1410, 1180 }, w.Games.HighScores);
            CollectionAssert.AreEqual(new[] { 2360, 1980, 1740, 1520, 1300 }, w.Games.RushHighScores);
            Assert.GreaterOrEqual(w.Economy.Balance, 340);
            Assert.AreEqual(3, w.Economy.BoosterCount(Booster.Shuffle));
            Assert.GreaterOrEqual(w.Level.TotalXP, 4820);
            Assert.AreEqual(7, w.Puzzles.CompletedCount(1));
            Assert.AreEqual(12, new MirrorRepository(w.Store, w.Clock).GamesMeasured);
            Assert.AreEqual(17, new MirrorRepository(w.Store, w.Clock).RepertoireCount);
            Assert.AreEqual(6, new SeedRepository(w.Store, w.Clock).Current?.Chain);
            Assert.AreEqual(20, w.Games.Load()?.Score);
            Assert.IsTrue(w.Achievements.UnlockedIds.Contains("streak14"));
            Assert.IsTrue(new ThemeRepository(w.Store, w.Economy).Owns("ocean"));
            Assert.IsFalse(new FirstRunState(w.Store, w.Clock, null).IsPending);
            Assert.AreEqual(1794227962053L, new InterstitialPolicy(w.Store, w.Clock).LastShownAt.ToUnixTimeMilliseconds());
            // Jour epoch 20766 = 9 novembre 2026 : la partie du jour compte.
            Assert.IsTrue(w.Streak.HasPlayedToday());
            Assert.IsEmpty(report.Ignored, report.ToString());
        }

        [Test]
        public void NativeFilesAreNeverModified()
        {
            string[] files = Directory.GetFiles(LegacyFixtures.Android("emulator"), "*", SearchOption.AllDirectories)
                .Where(f => !f.EndsWith(".meta")).OrderBy(f => f).ToArray();
            string Hash() => string.Join(",", files.Select(f => Convert.ToBase64String(SHA256.Create().ComputeHash(File.ReadAllBytes(f)))));
            DateTime[] stamps = files.Select(File.GetLastWriteTimeUtc).ToArray();
            string before = Hash();
            Migrate(World(), LegacyFixtures.AndroidSource("emulator", LegacyFixtures.Paris));
            Assert.AreEqual(before, Hash());
            CollectionAssert.AreEqual(stamps, files.Select(File.GetLastWriteTimeUtc).ToArray());
            CollectionAssert.AreEqual(files, Directory.GetFiles(LegacyFixtures.Android("emulator"), "*", SearchOption.AllDirectories)
                .Where(f => !f.EndsWith(".meta")).OrderBy(f => f).ToArray());
        }

        [Test]
        public void StartOfEpochDayFollowsLocalMidnight()
        {
            Assert.AreEqual(new DateTimeOffset(2026, 11, 9, 0, 0, 0, TimeSpan.FromHours(1)), AndroidLegacyImporter.StartOfEpochDay(20766, LegacyFixtures.Paris));
            Assert.AreEqual(new DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.Zero), AndroidLegacyImporter.StartOfEpochDay(0, TimeZoneInfo.Utc));
        }

        [Test]
        public void UnusableKotlinValuesAreIgnoredWithAReason()
        {
            var prefs = new Dictionary<string, LegacyValue>
            {
                ["tenGO_streakLastPlayed"] = LegacyValue.OfString("hier"),
                ["tenGO_mirrorData"] = LegacyValue.OfString("{pas du json"),
                ["tenGO_seedGarden"] = LegacyValue.OfString("{\"chain\":5}"),
                ["tengo_saved_game"] = LegacyValue.OfString("{\"cells\":[],\"score\":3}"),
                ["tengo_high_scores"] = LegacyValue.OfString("12,abc,7"),
                ["tenGO_booster_Hint"] = LegacyValue.OfInt(9),
            };
            var ads = new Dictionary<string, LegacyValue> { [SaveKeys.InterstitialLastShownAt] = LegacyValue.OfLong(0) };
            LegacyConversion c = AndroidLegacyImporter.Convert(new AndroidLegacyData(prefs, null, ads), LegacyFixtures.Paris);
            Assert.AreEqual("jour epoch illisible", c.Ignored["tenGO_streakLastPlayed"]);
            Assert.AreEqual("miroir illisible", c.Ignored["tenGO_mirrorData"]);
            Assert.AreEqual("graine illisible", c.Ignored["tenGO_seedGarden"]);
            Assert.AreEqual("partie illisible", c.Ignored["tengo_saved_game"]);
            Assert.AreEqual("clé inconnue", c.Ignored["tenGO_booster_Hint"]);
            Assert.AreEqual("jamais (valeur par défaut)", c.Ignored["tengo_ads.xml:AdMob.interstitialLastShownAt"]);
            CollectionAssert.AreEqual(new long[] { 12, 7 }, c.Values[SaveKeys.HighScores].Longs);
            Assert.AreEqual(1, c.Values.Count);
        }

        // MARK: - iOS

        [Test]
        public void LivedIosDefaultsAreReadBackByCoreRepositories()
        {
            TestWorld w = World();
            LegacyMigrationReport report = Migrate(w, LegacyFixtures.IosSource("lived"));
            Assert.AreEqual(LegacyMigrationStatus.Imported, report.Status, report.ToString());
            Assert.AreEqual("ios", report.Source);
            Assert.AreEqual(10, w.Economy.Balance);
            Assert.AreEqual(20, w.Level.TotalXP);
            Assert.AreEqual(1, w.Streak.Current);
            Assert.AreEqual(815094000.0, w.Store.GetDouble(SaveKeys.StreakLastPlayed));
            CollectionAssert.AreEqual(new[] { 0, 0 }, w.Games.HighScores);
            Assert.AreEqual(20261031, w.Store.GetInt(SaveKeys.MissionsDayKey));
            StringAssert.StartsWith("[{\"claimed\":false,\"id\":\"moves35\"", w.Store.GetString(SaveKeys.MissionsProgress));
            Assert.AreEqual(6, new SeedRepository(w.Store, w.Clock).Current?.Chain);
            var interstitials = new InterstitialPolicy(w.Store, w.Clock);
            Assert.AreEqual(SwiftDate.FromReferenceSeconds(815137457.215448), interstitials.LastShownAt);
            Assert.AreEqual("20261031", w.Store.GetString(SaveKeys.InterstitialsTodayDay));
            Assert.IsFalse(new FirstRunState(w.Store, w.Clock, null).IsPending);
            Assert.AreEqual("clé étrangère au jeu (SDK, PlayerPrefs)", report.Ignored["IABTCF_gdprApplies"]);
            Assert.IsFalse(w.Store.Has("IABTCF_CmpSdkID"));
            // Missions d'un jour passé : renouvelées normalement par le dépôt.
            Assert.AreEqual(4, w.Missions.TodaysMissions.Count);
        }

        [Test]
        public void SeededIosDefaultsAreReadBackByCoreRepositories()
        {
            TestWorld w = World();
            LegacyMigrationReport report = Migrate(w, LegacyFixtures.IosSource("seeded"));
            Assert.AreEqual(LegacyMigrationStatus.Imported, report.Status, report.ToString());
            Assert.AreEqual(340, w.Economy.Balance);
            Assert.AreEqual(2, w.Economy.BoosterCount(Booster.Hammer));
            Assert.AreEqual(4820, w.Level.TotalXP);
            Assert.AreEqual(12, w.Streak.Current);
            Assert.AreEqual(1, w.Streak.ShieldCount);
            Assert.AreEqual(1463, w.Stats.TotalChainsMade);
            CollectionAssert.AreEqual(new[] { 2140, 1980, 1760 }, w.Games.RushHighScores);
            var mirror = new MirrorRepository(w.Store, w.Clock);
            Assert.AreEqual(12, mirror.GamesMeasured);
            Assert.AreEqual(17, mirror.RepertoireCount);
            Assert.AreEqual(7, new SeedRepository(w.Store, w.Clock).Current?.Chain);
            Assert.IsTrue(new AdFreeRepository(w.Store, w.Economy).IsPurchased);
            var firstRun = new FirstRunState(w.Store, w.Clock, null);
            Assert.IsTrue(firstRun.IsPending);
            Assert.AreEqual(815920828.015542, w.Store.GetDouble(SaveKeys.FirstOpenAt), 1e-6);
            Assert.IsTrue(w.Store.GetBool(SaveKeys.BoosterCoachSeen("shuffle")));
            Assert.IsEmpty(report.Ignored, report.ToString());
        }

        [Test]
        public void IosValuesAreNormalizedToTheExpectedType()
        {
            var defaults = new Dictionary<string, LegacyValue>
            {
                [SaveKeys.HasSeenTutorial] = LegacyValue.OfInt(1),       // NSNumber entier pour un booléen
                [SaveKeys.CoinsBalance] = LegacyValue.OfString("75"),     // nombre en chaîne
                [SaveKeys.OwnedThemes] = LegacyValue.OfIntList(new long[0]), // tableau vide
                [SaveKeys.SeedGarden] = LegacyValue.OfBytes(new byte[] { 0xFF }), // NSData non UTF-8
                [SaveKeys.TotalXP] = LegacyValue.OfLong(5_000_000_000L),
                ["tenGO_puzzleStars_x_1"] = LegacyValue.OfInt(3),
            };
            var store = new MemorySaveStore();
            var report = LegacyMigration.Run(store, new FixedSource(IosLegacyImporter.Convert(defaults)), "4.0.0");
            Assert.IsTrue(store.GetBool(SaveKeys.HasSeenTutorial));
            Assert.AreEqual(75, store.GetInt(SaveKeys.CoinsBalance));
            CollectionAssert.IsEmpty(store.GetStringList(SaveKeys.OwnedThemes));
            Assert.IsFalse(store.Has(SaveKeys.SeedGarden));
            Assert.IsFalse(store.Has(SaveKeys.TotalXP));
            Assert.AreEqual("entier hors limites", report.Ignored[SaveKeys.TotalXP]);
            StringAssert.Contains("étrangère", report.Ignored["tenGO_puzzleStars_x_1"]);
        }

        // MARK: - Une seule fois, sans écraser

        [Test]
        public void SecondLaunchDoesNothing()
        {
            TestWorld w = World();
            Assert.AreEqual(LegacyMigrationStatus.Imported, Migrate(w, LegacyFixtures.IosSource("seeded")).Status);
            Assert.IsTrue(LegacyMigration.IsDone(w.Store));
            Assert.AreEqual("4.0.0", w.Store.GetString(LegacyMigration.VersionKey));
            Assert.AreEqual("ios", w.Store.GetString(LegacyMigration.SourceKey));
            w.Economy.SpendCoins(40);
            string snapshot = SaveStoreJson.ToJson(w.Store);
            var calls = new CountingSource(LegacyFixtures.IosSource("seeded"));
            LegacyMigrationReport again = Migrate(w, calls);
            Assert.AreEqual(LegacyMigrationStatus.AlreadyDone, again.Status);
            Assert.AreEqual(0, calls.Reads);
            Assert.AreEqual(snapshot, SaveStoreJson.ToJson(w.Store));
            Assert.AreEqual(300, w.Economy.Balance);
        }

        [Test]
        public void ExistingUnityValuesAreKept()
        {
            TestWorld w = World();
            w.Store.SetInt(SaveKeys.CoinsBalance, 999);
            w.Store.SetIntList(SaveKeys.HighScores, new[] { 50 });
            LegacyMigrationReport report = Migrate(w, LegacyFixtures.AndroidSource("complete", LegacyFixtures.Paris));
            Assert.AreEqual(999, w.Economy.Balance);
            CollectionAssert.AreEqual(new[] { 50 }, w.Games.HighScores);
            CollectionAssert.AreEquivalent(new[] { SaveKeys.CoinsBalance, SaveKeys.HighScores }, report.Kept);
            Assert.AreEqual(15000, w.Level.TotalXP);
        }

        [Test]
        public void FreshInstallMarksDoneWithoutData()
        {
            var store = new MemorySaveStore();
            string dir = Path.Combine(Path.GetTempPath(), "tengo-legacy-" + Guid.NewGuid().ToString("N"));
            var report = LegacyMigration.Run(store, new AndroidFilesLegacySource(Path.Combine(dir, "files"), Path.Combine(dir, "shared_prefs"), TimeZoneInfo.Utc), "4.0.0");
            Assert.AreEqual(LegacyMigrationStatus.NoNativeData, report.Status);
            Assert.IsTrue(LegacyMigration.IsDone(store));
            Assert.AreEqual(LegacyMigrationStatus.NoNativeData, LegacyMigration.Run(new MemorySaveStore(), null, "4.0.0").Status);
            Assert.AreEqual(LegacyMigrationStatus.NoNativeData, LegacyMigration.Run(new MemorySaveStore(), new IosJsonLegacySource(_ => "{}"), "4.0.0").Status);
        }

        [Test]
        public void ReadFailureIsRetriedThenAbandoned()
        {
            var store = new MemorySaveStore();
            var failing = new IosJsonLegacySource(_ => "!serialisation");
            var logs = new List<string>();
            Assert.AreEqual(LegacyMigrationStatus.Failed, LegacyMigration.Run(store, failing, "4.0.0", logs.Add).Status);
            Assert.IsFalse(LegacyMigration.IsDone(store));
            Assert.AreEqual(LegacyMigrationStatus.Failed, LegacyMigration.Run(store, failing, "4.0.0").Status);
            Assert.AreEqual(LegacyMigrationStatus.GaveUp, LegacyMigration.Run(store, failing, "4.0.0").Status);
            Assert.IsTrue(LegacyMigration.IsDone(store));
            Assert.IsFalse(store.Has(LegacyMigration.AttemptsKey));
            StringAssert.Contains("FormatException", logs[0]);

            // Un essai réussi après un échec efface le compteur.
            var store2 = new MemorySaveStore();
            LegacyMigration.Run(store2, failing, "4.0.0");
            Assert.AreEqual(LegacyMigrationStatus.Imported, LegacyMigration.Run(store2, LegacyFixtures.IosSource("lived"), "4.0.0").Status);
            Assert.IsFalse(store2.Has(LegacyMigration.AttemptsKey));
        }

        [Test]
        public void ReportNeverContainsValues()
        {
            var logs = new List<string>();
            LegacyMigration.Run(new MemorySaveStore(), LegacyFixtures.AndroidSource("complete", LegacyFixtures.Paris), "4.0.0", logs.Add);
            Assert.AreEqual(1, logs.Count);
            StringAssert.Contains("tenGO_duelHistory", logs[0]);
            foreach (string secret in new[] { "K7Q2MX", "1234", "15000", "night", "é€" })
                StringAssert.DoesNotContain(secret, logs[0]);
        }

        private sealed class FixedSource : ILegacySource
        {
            private readonly LegacyConversion _conversion;
            public FixedSource(LegacyConversion conversion) { _conversion = conversion; }
            public string Name => _conversion.Source;
            public LegacyConversion Read() => _conversion;
        }

        private sealed class CountingSource : ILegacySource
        {
            private readonly ILegacySource _inner;
            public CountingSource(ILegacySource inner) { _inner = inner; }
            public int Reads { get; private set; }
            public string Name => _inner.Name;
            public LegacyConversion Read() { Reads++; return _inner.Read(); }
        }
    }
}
