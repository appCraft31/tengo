using System;
using System.Linq;
using NUnit.Framework;
using TenGo.App;
using TenGo.Core;
using TenGo.Fx;
using TenGo.Game;
using TenGo.Haptics;
using TenGo.Rendering;
using TenGo.Screens;
using TenGo.Services;
using UnityEngine;

namespace TenGo.Tests
{
    /// Étape 6 : écrans hors partie de bout en bout (navigation, boutique,
    /// missions, succès, duel, puzzles, réglages, premier lancement, liens
    /// profonds, ordre du consentement). Gestes injectés, temps avancé à la main.
    public class ScreenFlowTests
    {
        const float Dt = 1f / 60f;
        AppRoot _app;
        MemorySaveStore _store;
        LogAnalytics _analytics;

        AppServices S => _app.Services;

        [SetUp]
        public void SetUp()
        {
            FxTime.Reset();
            JuiceSettings.Reset();
            JuiceSettings.SystemReduceMotion = () => false;
            Look.Reset();
            HapticManager.Current = new NullHaptics();
            _store = new MemorySaveStore();
            _store.SetBool(SaveKeys.HasSeenTutorial, true);
            _store.SetBool(SaveKeys.HasSeenShopPurchaseGuide, true);
            _store.SetInt(SaveKeys.NotifGamesPlayed, 5);
            _store.SetInt(SaveKeys.CoinsBalance, 340);
            _analytics = new LogAnalytics { Echo = false };
        }

        [TearDown]
        public void TearDown()
        {
            Destroy();
            FxTime.Reset();
            JuiceSettings.Reset();
            HapticManager.Reset();
            Look.Reset();
            Audio.SoundManager.ResetShared();
        }

        void Destroy()
        {
            if (_app != null) UnityEngine.Object.DestroyImmediate(_app.gameObject);
            _app = null;
        }

        void Boot(string launchUrl = null, bool firstInstall = false)
        {
            if (firstInstall)
            {
                _store = new MemorySaveStore();
            }
            _app = AppRoot.Create(new AppRoot.Options
            {
                Store = _store,
                Clock = new FixedClock(new DateTimeOffset(2026, 11, 9, 10, 0, 0, TimeSpan.Zero), TimeZoneInfo.Utc),
                Geometry = SceneGeometry.IPhone17ProMax,
                AutoTick = false,
                ReadDevices = false,
                Language = "fr",
                Fades = false,
                LaunchUrl = launchUrl,
                Services = new AppServices.Overrides
                {
                    Analytics = _analytics,
                    Interstitials = new SimulatedInterstitialAds { Ready = false },
                },
            });
            Advance(0.5f);
        }

        void Reboot(string launchUrl = null)
        {
            Destroy();
            Look.Reset();
            Boot(launchUrl);
        }

        void Step() { FxTime.Step(Dt); _app.Tick(Dt); }
        void Advance(float s) { for (int i = 0, n = Mathf.CeilToInt(s / Dt); i < n; i++) Step(); }

        void Tap(Vector2 pt)
        {
            _app.Input.InjectDown(pt); Step();
            _app.Input.InjectUp(pt); Step();
        }

        /// Tap géométrique sur une cible nommée de l'écran courant (vrai geste).
        void TapNamed(string name)
        {
            var screen = _app.Current as ScreenBase;
            Assert.IsNotNull(screen, "écran courant : " + _app.Current);
            var p = screen.TargetPoint(name);
            Assert.IsTrue(p.HasValue, $"cible « {name} » absente de {screen} ({string.Join(", ", screen.TargetNames())})");
            Tap(p.Value);
            Advance(0.3f);
        }

        void TapTab(Tab tab)
        {
            var bar = _app.Current.Root.Find(TabBar.NodeName);
            Assert.IsNotNull(bar, "barre d'onglets absente de " + _app.Current);
            Tap(TabBar.TabCenter(bar, tab));
            Advance(0.2f);
        }

        // MARK: - Navigation

        [Test]
        public void Tabs_NavigateEachScreen_AndBackReturnsToMenu()
        {
            Boot();
            Assert.IsInstanceOf<MenuScreen>(_app.Current);
            TapTab(Tab.Progress);
            Assert.IsInstanceOf<ProfileScreen>(_app.Current);
            TapTab(Tab.Social);
            Assert.IsInstanceOf<DuelScreen>(_app.Current);
            TapTab(Tab.Shop);
            Assert.IsInstanceOf<ShopScreen>(_app.Current);
            TapTab(Tab.Play);
            Assert.IsInstanceOf<MenuScreen>(_app.Current);

            foreach (Action open in new Action[]
                     {
                         () => _app.ShowProfile(), () => _app.ShowMissions(), () => _app.ShowAchievements(), () => _app.ShowMirror(),
                         () => _app.ShowLeaderboard(), () => _app.ShowDuel(), () => _app.ShowPuzzleLevels(1), () => _app.ShowShop(false),
                     })
            {
                open();
                Advance(0.4f);
                var name = _app.Current.ToString();
                _app.Current.Back();
                Advance(0.4f);
                // Le Miroir revient au Profil comme iOS ; tout le reste à l'accueil.
                if (_app.Current is ProfileScreen) { _app.Current.Back(); Advance(0.4f); }
                Assert.IsInstanceOf<MenuScreen>(_app.Current, "retour depuis " + name);
            }
        }

        [Test]
        public void Menu_TilesOpenTheirScreens()
        {
            Boot();
            TapNamed("puzzles");
            Assert.IsInstanceOf<PuzzleLevelsScreen>(_app.Current);
            _app.ShowMenu();
            TapNamed("duel");
            Assert.IsInstanceOf<DuelScreen>(_app.Current);
            _app.ShowMenu();
            TapNamed("levelChip");
            Assert.IsInstanceOf<ProfileScreen>(_app.Current);
            _app.ShowMenu();
            TapNamed("newGame");
            Assert.IsNotNull(_app.Game);
            Assert.AreEqual(GameModeKind.Normal, _app.Game.Kind);
        }

        // MARK: - Boutique

        [Test]
        public void Shop_BuyAndEquipTheme_ThenSwitchBack()
        {
            Boot();
            _app.ShowShop(false);
            Advance(0.3f);
            var forest = ThemeCatalog.ById("forest");
            Assert.IsFalse(S.Themes.Owns("forest"));
            ((ShopScreen)_app.Current).TapTarget("item:theme:forest");
            Advance(0.2f);
            Assert.AreEqual("forest", S.Themes.ActiveId);
            Assert.AreEqual(340 - forest.Price, S.Economy.Balance);
            Assert.AreEqual("forest", Look.Theme.Id, "le thème actif est appliqué au rendu");

            ((ShopScreen)_app.Current).TapTarget("item:theme:default");
            Advance(0.2f);
            Assert.AreEqual(Theme.DefaultId, S.Themes.ActiveId);
            Assert.IsTrue(S.Themes.Owns("forest"));
            Assert.AreEqual(340 - forest.Price, S.Economy.Balance, "rééquiper ne recoûte rien");

            // Trop cher : refus, rien ne change.
            ((ShopScreen)_app.Current).TapTarget("item:theme:night");
            Advance(0.3f);
            Assert.IsFalse(S.Themes.Owns("night"));
            Assert.AreEqual(Theme.DefaultId, S.Themes.ActiveId);
        }

        [Test]
        public void Shop_CosmeticAndBoosterPurchases()
        {
            Boot();
            _app.ShowShop(false);
            Advance(0.3f);
            var shop = (ShopScreen)_app.Current;
            shop.TapTarget("item:trail:neon");
            Advance(0.2f);
            Assert.AreEqual("neon", S.Cosmetics.ActiveId(CosmeticKind.Trail));
            Assert.AreEqual(TrailKind.Neon, Look.Trail);

            int before = S.Economy.BoosterCount(Booster.Hint);
            int balance = S.Economy.Balance;
            ((ShopScreen)_app.Current).TapTarget("booster:hint:1");
            Advance(0.2f);
            Assert.AreEqual(before + 1, S.Economy.BoosterCount(Booster.Hint));
            Assert.AreEqual(balance - Booster.Hint.Price(), S.Economy.Balance);
            Assert.AreEqual(1, _analytics.Count("spend_virtual_currency"));
        }

        [Test]
        public void Shop_NoAdsPurchase_AndRestoreFromSettings()
        {
            _store.SetBool(SaveKeys.NoAdsPurchased, false);
            Boot();
            _app.ShowShop(false);
            Advance(0.3f);
            Assert.AreEqual(1, _analytics.Count("noads_offer_view"));
            TapNamed("noAds");
            Advance(0.6f);
            Assert.IsTrue(S.AdFree.IsPurchased);
            Assert.AreEqual(340 + StoreCatalog.AdFreeBonusCoins, S.Economy.Balance);
            StringAssert.Contains("500", ((ShopScreen)_app.Current).LastToast);

            // Restauration (réinstallation) : le compte du store possède le mod.
            _store.SetBool(SaveKeys.NoAdsPurchased, false);
            ((SimulatedCoinStore)S.CoinStore).OwnsAdFree = true;
            _app.ShowMenu();
            Advance(0.2f);
            var menu = (MenuScreen)_app.Current;
            menu.OpenSettings();
            Advance(0.3f);
            Assert.IsTrue(menu.SettingsOpen);
            var settings = (SettingsOverlay)menu.CurrentOverlay;
            settings.TapRow("row_restore");
            Advance(0.6f);
            Assert.IsTrue(S.AdFree.IsPurchased);
            Assert.AreEqual(Loc.T("settings.restore_done"), settings.AlertMessage);
        }

        [Test]
        public void Shop_CoinPackPurchase_AddsCoins()
        {
            Boot();
            _app.ShowShop(false);
            Advance(0.3f);
            var pack = S.CoinStore.Packs[0];
            ((ShopScreen)_app.Current).TapTarget("pack:" + pack.ProductId);
            Advance(0.6f);
            Assert.AreEqual(340 + pack.Coins, S.Economy.Balance);
        }

        [Test]
        public void ShopGuide_FromMenu_GuidedPurchaseIsRefunded()
        {
            _store.SetBool(SaveKeys.HasSeenShopPurchaseGuide, false);
            _store.SetInt(SaveKeys.CoinsBalance, 45);
            Boot();
            Advance(0.6f);
            var menu = (MenuScreen)_app.Current;
            Assert.IsTrue(menu.ShopGuideVisible, "coach-mark sur l'onglet Boutique");
            Tap(TabBar.TabCenter(menu.Root.Find(TabBar.NodeName), Tab.Shop));
            Advance(0.4f);
            var shop = _app.Current as ShopScreen;
            Assert.IsNotNull(shop);
            Assert.IsTrue(shop.GuideVisible);
            int hints = S.Economy.BoosterCount(Booster.Hint);
            shop.GuideTap(true);
            Assert.AreEqual(hints + 1, S.Economy.BoosterCount(Booster.Hint));
            Assert.AreEqual(45 - Booster.Hint.Price(), S.Economy.Balance);
            Advance(1.6f);
            Assert.AreEqual(45, S.Economy.Balance, "le premier indice est remboursé");
            Assert.IsTrue(S.Economy.ShopGuideCompleted);
            Assert.AreEqual(1, _analytics.Count("earn_virtual_currency"));
        }

        [Test]
        public void ShopGuide_Later_SnoozesWithoutPurchase()
        {
            _store.SetBool(SaveKeys.HasSeenShopPurchaseGuide, false);
            _store.SetInt(SaveKeys.CoinsBalance, 45);
            Boot();
            _app.ShowShop(false, true);
            Advance(0.4f);
            var shop = (ShopScreen)_app.Current;
            shop.GuideTap(false);
            Assert.IsFalse(shop.GuideVisible);
            Assert.AreEqual(1, S.Economy.ShopGuideSnooze);
            Assert.AreEqual(45, S.Economy.Balance);
            Assert.IsFalse(S.Economy.ShopGuideCompleted);
        }

        [Test]
        public void Shop_FromGame_BackResumesGame()
        {
            Boot();
            S.Games.Save(GridModel.Generate(new SeededGenerator(7)), 420);
            _app.ShowShop(true);
            Advance(0.3f);
            Assert.IsNull(_app.Current.Root.Find(TabBar.NodeName), "pas d'onglets depuis une partie");
            TapNamed("shopBack");
            Assert.IsNotNull(_app.Game);
            Assert.AreEqual(420, _app.Game.Score);
        }

        // MARK: - Progression

        [Test]
        public void Missions_CompletedMissionIsClaimed()
        {
            Boot();
            S.Missions.EnsureRolled(DayKeys.Challenge(S.Clock));
            for (int i = 0; i < 200; i++) { S.Missions.ReportChain(9); S.Missions.ReportMove(); S.Missions.ReportPerfectBoard(); S.Missions.ReportGameEnded(true); }
            S.Missions.ReportScore(100000);
            var done = S.Missions.TodaysMissions.First(m => m.IsCompleted && !m.Claimed);
            _app.ShowMissions();
            Advance(0.3f);
            int balance = S.Economy.Balance;
            Assert.IsTrue(((MissionsScreen)_app.Current).Claim(done.Definition.Id));
            Advance(0.4f);
            Assert.IsTrue(S.Missions.TodaysMissions.First(m => m.Definition.Id == done.Definition.Id).Claimed);
            Assert.Greater(S.Economy.Balance, balance);

            // Réclamée depuis le Profil aussi (une autre mission terminée).
            var other = S.Missions.TodaysMissions.FirstOrDefault(m => m.IsCompleted && !m.Claimed);
            if (other != null)
            {
                _app.ShowProfile();
                Advance(0.3f);
                Assert.IsTrue(((ProfileScreen)_app.Current).Claim(other.Definition.Id));
                Assert.IsTrue(S.Missions.TodaysMissions.First(m => m.Definition.Id == other.Definition.Id).Claimed);
            }
        }

        [Test]
        public void Achievements_DisplayEachCategory()
        {
            Boot();
            _app.ShowAchievements();
            Advance(0.3f);
            var screen = (AchievementsScreen)_app.Current;
            var names = screen.TargetNames().ToList();
            foreach (var c in new[] { "category_chain", "category_perfect", "category_score", "category_daily" })
                CollectionAssert.Contains(names, c);
            TapNamed("category_score");
            Advance(0.4f);
            Assert.AreEqual(AchievementCategory.Score, ((AchievementsScreen)_app.Current).Category);
            int texts = _app.Current.Root.GetComponentsInChildren<TenGoText>().Length;
            Assert.Greater(texts, 10, "les succès de la catégorie sont affichés");
        }

        // MARK: - Duel et puzzles

        [Test]
        public void Duel_CreateAndJoinStartDuelGames()
        {
            Boot();
            _app.ShowDuel();
            Advance(0.3f);
            ((DuelScreen)_app.Current).TapTarget("startDuel");
            Advance(0.5f);
            Assert.IsNotNull(_app.Game, "créer un duel lance la partie");
            Assert.AreEqual(GameModeKind.Duel, _app.Game.Kind);

            _app.ShowDuel();
            Advance(0.3f);
            var duel = (DuelScreen)_app.Current;
            duel.TapTarget("answerDuel");
            Advance(0.4f);
            Assert.IsNotNull(duel.CodeEntry, "saisie du code ouverte");
            foreach (char c in "K7MQ2P") duel.CodeEntry.Type(c);
            duel.CodeEntry.Submit();
            Advance(0.6f);
            Assert.IsNotNull(_app.Game, "rejoindre un duel lance la partie");
            Assert.AreEqual(GameModeKind.Duel, _app.Game.Kind);
        }

        [Test]
        public void Duel_JoinRefusalsAreShownWithoutStartingAGame()
        {
            Boot();
            var backend = (SimulatedDuelBackend)S.Duels;
            backend.Seed(new Duel { Code = "MYWNZ2", Seed = 9, ChallengerUid = backend.Uid, ChallengerName = "Moi", ChallengerScore = 50,
                ExpiresAt = System.DateTimeOffset.UtcNow.AddDays(1) });
            backend.Seed(new Duel { Code = "LDXNE3", Seed = 9, ChallengerUid = "rival", ChallengerScore = 50,
                ExpiresAt = System.DateTimeOffset.UtcNow.AddDays(-1) });
            _app.ShowDuel();
            Advance(0.3f);
            var duel = (DuelScreen)_app.Current;
            foreach (var (code, key) in new[] { ("MYWNZ2", "duel.error_own_duel"), ("LDXNE3", "duel.error_expired") })
            {
                duel.JoinWithCode(code);
                Advance(0.6f);
                Assert.IsNull(_app.Game, "refus : pas de partie pour " + code);
                Assert.AreEqual(Loc.T(key), duel.StatusText);
            }
            backend.InventsUnknownDuels = false;
            duel.JoinWithCode("ZZZZZZ");
            Advance(0.6f);
            Assert.AreEqual(Loc.T("duel.error_not_found"), duel.StatusText);
        }

        [Test]
        public void Duel_ListReloadsFromServerWithOutcomes()
        {
            Boot();
            var backend = (SimulatedDuelBackend)S.Duels;
            backend.Seed(new Duel { Code = "WNWNWN", Seed = 1, ChallengerUid = backend.Uid, ChallengerScore = 900, OpponentUid = "rival",
                OpponentName = "Rival", OpponentScore = 400, ExpiresAt = System.DateTimeOffset.UtcNow.AddDays(2) });
            backend.Seed(new Duel { Code = "WAITME", Seed = 2, ChallengerUid = backend.Uid, ChallengerScore = 300,
                ExpiresAt = System.DateTimeOffset.UtcNow.AddDays(2) });
            S.DuelHistory.Remember("WNWNWN");
            S.DuelHistory.Remember("WAITME");
            _app.ShowDuel();
            Advance(0.8f);
            var duel = (DuelScreen)_app.Current;
            Assert.AreEqual(2, duel.Duels.Count);
            Assert.IsNotNull(duel.FindTarget("duelShare:WAITME"), "duel lancé sans adversaire : partageable");
            Assert.IsNull(duel.FindTarget("duelShare:WNWNWN"), "duel terminé : pas de partage");
            duel.TapTarget("duelShare:WAITME");
            Assert.IsTrue(((LogPlayServices)S.Play).Calls.Contains("partage du duel WAITME"));
        }

        [Test]
        public void PuzzleLevels_SelectUnlockedLevel_LockedRefused()
        {
            Boot();
            _app.ShowPuzzleLevels(1);
            Advance(0.3f);
            var screen = (PuzzleLevelsScreen)_app.Current;
            Assert.AreEqual(1, screen.NextLevelIndex);
            Assert.IsFalse(screen.SelectLevel(5), "niveau verrouillé");
            Assert.IsInstanceOf<PuzzleLevelsScreen>(_app.Current);
            TapNamed("level_1");
            Advance(0.3f);
            Assert.IsNotNull(_app.Game);
            Assert.AreEqual(GameModeKind.Puzzle, _app.Game.Kind);
        }

        // MARK: - Réglages

        [Test]
        public void Settings_TogglesArePersisted()
        {
            Boot();
            var menu = (MenuScreen)_app.Current;
            TapNamed("parametres");
            Advance(0.3f);
            Assert.IsTrue(menu.SettingsOpen);
            var overlay = (SettingsOverlay)menu.CurrentOverlay;
            Assert.IsTrue(overlay.TapRow("row_sound"));
            Assert.IsTrue(overlay.TapRow("row_haptic"));
            Assert.IsTrue(overlay.TapRow("row_effects"));
            Assert.AreEqual(1, _analytics.Count("sound_toggled"));
            overlay.TapRow("closeButton");
            Advance(0.4f);
            Assert.IsFalse(menu.SettingsOpen);

            Reboot();
            Assert.IsTrue(S.Settings.SoundMuted);
            Assert.IsFalse(S.Settings.HapticsEnabled);
            Assert.IsTrue(S.Settings.ReducedEffects);
            Assert.AreEqual(JuiceLevel.Reduced, JuiceSettings.Level);
        }

        [Test]
        public void Settings_ReplayTutorialOpensTutorial()
        {
            Boot();
            var menu = (MenuScreen)_app.Current;
            menu.OpenSettings();
            Advance(0.3f);
            var overlay = (SettingsOverlay)menu.CurrentOverlay;
            overlay.TapRow("row_tutorial");
            Advance(0.5f);
            var tuto = _app.Current as TutorialScreen;
            Assert.IsNotNull(tuto);
            Assert.IsFalse(tuto.FirstRun);
            Assert.AreEqual(3, tuto.TotalSteps);
        }

        // MARK: - Premier lancement

        [Test]
        public void FirstRun_ShortTutorialThenGame_ThenNeverAgain()
        {
            Boot(firstInstall: true);
            var tuto = _app.Current as TutorialScreen;
            Assert.IsNotNull(tuto, "premier écran : tutoriel");
            Assert.IsTrue(tuto.FirstRun);
            Assert.AreEqual(2, tuto.TotalSteps);
            Assert.IsFalse(_app.ConsentRequested, "pas de consentement avant la première partie");

            TapNamed("next");
            Advance(0.4f);
            Assert.AreEqual(1, tuto.Step);
            TapNamed("next");
            Advance(0.4f);
            Assert.IsNotNull(_app.Game, "le tutoriel mène droit en partie");
            Assert.IsTrue(S.Settings.HasSeenTutorial);
            Assert.AreEqual(1, _analytics.Count("tutorial_complete"));

            // Fin de la première partie → consentement 1,6 s plus tard, Game Center après.
            S.FirstRun.GameCompleted("normal", 300, false);
            Assert.IsTrue(_app.ConsentRequested);
            var play = (LogPlayServices)S.Play;
            Assert.IsFalse(play.Calls.Any(c => c.Contains("Game Center")), "Game Center attend la réponse");
            Advance(2.2f);
            Assert.IsTrue(_app.AdsSessionStarted);
            Assert.IsTrue(play.Calls.Any(c => c.Contains("Game Center")));

            Reboot();
            Assert.IsInstanceOf<MenuScreen>(_app.Current, "plus jamais de tutoriel au lancement");
        }

        [Test]
        public void FirstRun_ClosingTutorialStillStartsGame()
        {
            Boot(firstInstall: true);
            TapNamed("close");
            Advance(0.3f);
            Assert.IsNotNull(_app.Game);
            Assert.IsTrue(S.Settings.HasSeenTutorial);
        }

        [Test]
        public void ReturningPlayer_GameCenterThenConsent()
        {
            Boot();
            Assert.IsTrue(_app.ConsentRequested);
            var play = (LogPlayServices)S.Play;
            Assert.IsTrue(play.Calls.Any(c => c.Contains("Game Center")));
            Advance(1f);
            Assert.IsTrue(_app.AdsSessionStarted);
        }

        // MARK: - Liens profonds

        [Test]
        public void DeepLink_AtLaunch_OpensDestination()
        {
            Boot("tengo://progress");
            Assert.IsInstanceOf<ProfileScreen>(_app.Current);
            Reboot("https://appcraft31.app/tengo/daily");
            Assert.IsNotNull(_app.Game);
            Assert.AreEqual(GameModeKind.Daily, _app.Game.Kind);
            Reboot("tengo://duel/k7mq2p");
            Assert.IsNotNull(_app.Game, "un code de duel reçu lance la partie, comme iOS");
            Assert.AreEqual(GameModeKind.Duel, _app.Game.Kind);
        }

        [Test]
        public void DeepLink_WhileRunning_RoutesAndIgnoresUnknown()
        {
            Boot();
            Assert.IsFalse(_app.HandleDeepLink("tengo://nowhere"));
            Assert.IsInstanceOf<MenuScreen>(_app.Current);
            Assert.IsTrue(_app.HandleDeepLink("tengo://progress"));
            Advance(0.3f);
            Assert.IsInstanceOf<ProfileScreen>(_app.Current);
            Assert.IsTrue(_app.HandleDeepLink("https://appcraft31.app/tengo/duel/B4XN9T"));
            Advance(0.6f);
            Assert.AreEqual(GameModeKind.Duel, _app.Game.Kind);
        }
    }
}
