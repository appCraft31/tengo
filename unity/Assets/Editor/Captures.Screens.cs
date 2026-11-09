using System;
using System.Collections.Generic;
using System.Linq;
using TenGo.App;
using TenGo.Core;
using TenGo.Fx;
using TenGo.Rendering;
using TenGo.Screens;
using TenGo.Services;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TenGo.Editor
{
    /// Étape 6 : chaque écran hors partie, sur l'iPhone 17 Pro Max simulé,
    /// avec les données de vitrine d'iOS (`SCREENSHOT_SEED_DATA`), dans
    /// Logs/captures/screens/<langue>_<thème>_<écran>.png.
    ///   Tools/unity.sh TenGo.Editor.Captures.Screens [-langs fr,en,ja] [-themes default,night] [-only menu,shop]
    public static partial class Captures
    {
        public const string ScreensDir = "screens";

        /// Une capture : nom, écran à construire, préparation de la sauvegarde, gestes après affichage.
        public sealed class ScreenShot
        {
            public string Name;
            public Func<AppRoot, IAppScreen> Make;
            public Action<ISaveStore> Seed;
            public Action<AppRoot> After;
            public float Settle = 1.2f;
        }

        static AppRoot _shotApp;

        public static readonly List<ScreenShot> ScreenShots = new()
        {
            new() { Name = "menu", Make = a => a.Screens.Menu(a) },
            new() { Name = "menu_resume_seed", Make = a => a.Screens.Menu(a), Seed = SeedResumeAndBloom },
            new() { Name = "menu_resume_seed_growing", Make = a => a.Screens.Menu(a), Seed = SeedResumeAndGrowing },
            new() { Name = "settings", Make = a => a.Screens.Menu(a), After = a => (a.Current as MenuScreen)?.OpenSettings() },
            new() { Name = "shop", Make = a => a.Screens.Shop(a, false) },
            new() { Name = "shop_bubbles", Make = a => a.Screens.Shop(a, false), After = a => (a.Current as ShopScreen)?.ScrollToSection("bubbles") },
            new() { Name = "shop_trails", Make = a => a.Screens.Shop(a, false), After = a => (a.Current as ShopScreen)?.ScrollToSection("trails") },
            new() { Name = "shop_boosters", Make = a => a.Screens.Shop(a, false), After = a => (a.Current as ShopScreen)?.ScrollToSection("boosters") },
            new() { Name = "shop_coins", Make = a => a.Screens.Shop(a, false), After = a => (a.Current as ShopScreen)?.ScrollToSection("coins") },
            new() { Name = "shop_noads", Make = a => a.Screens.Shop(a, false), Seed = s => s.SetBool(SaveKeys.NoAdsPurchased, false) },
            new() { Name = "shop_guided", Make = a => a.Screens.Shop(a, false, true), Seed = s => { s.SetBool(SaveKeys.HasSeenShopPurchaseGuide, false); s.SetInt(SaveKeys.CoinsBalance, 45); } },
            new() { Name = "shop_from_game", Make = a => a.Screens.Shop(a, true) },
            new() { Name = "profile", Make = a => a.Screens.Profile(a) },
            new() { Name = "missions", Make = a => a.Screens.Missions(a) },
            new() { Name = "achievements", Make = a => a.Screens.Achievements(a) },
            new() { Name = "mirror", Make = a => a.Screens.Mirror(a), Seed = s => SeedMirror(s) },
            new() { Name = "mirror_empty", Make = a => a.Screens.Mirror(a) },
            // Missions entamées (une à réclamer, une réclamée) et miroir rempli, comme la référence iOS.
            new() { Name = "profile_progress", Seed = s => SeedMirror(s), Make = a =>
            {
                var m = a.Services.Missions;
                m.ReportScore(3200);
                for (int i = 0; i < 12; i++) m.ReportChain(4);
                var done = m.TodaysMissions.FirstOrDefault(x => x.IsCompleted);
                if (done != null) m.Claim(done.Definition.Id);
                m.ReportScore(3000);
                return a.Screens.Profile(a);
            } },
            new() { Name = "missions_claim", Make = a =>
            {
                var m = a.Services.Missions;
                m.ReportScore(3200);
                for (int i = 0; i < 12; i++) m.ReportChain(4);
                var done = m.TodaysMissions.FirstOrDefault(x => x.IsCompleted);
                if (done != null) m.Claim(done.Definition.Id);
                m.ReportScore(3000);
                return a.Screens.Missions(a);
            } },
            new() { Name = "achievements_score", Make = a => a.Screens.Achievements(a),
                After = a => (a.Current as AchievementsScreen)?.SelectCategory(AchievementCategory.Score) },
            new() { Name = "duel", Make = a => a.Screens.Duel(a) },
            new() { Name = "duel_incoming", Make = a => a.Screens.Duel(a, "K7MQ2P") },
            new() { Name = "puzzles", Make = a => a.Screens.PuzzleLevels(a, 1) },
            new() { Name = "duel_code_entry", Make = a => a.Screens.Duel(a), After = a => { TapNamed(a, "answerDuel"); Run(0.4f); var e = (a.Current as DuelScreen)?.CodeEntry; if (e != null) foreach (var c in "K7M") e.Type(c); } },
            new() { Name = "duel_empty", Make = a => a.Screens.Duel(a), Seed = s => s.Remove(SaveKeys.DuelHistory) },
            new() { Name = "puzzles_fresh", Make = a => a.Screens.PuzzleLevels(a, 1), Seed = s => { for (int i = 1; i <= 20; i++) s.Remove(SaveKeys.PuzzleStars(1, i)); } },
            new() { Name = "leaderboard", Make = a => a.Screens.Leaderboard(a) },
            // Tutoriel jouable (4.0) : chaque étape, un tracé en cours, la fin avec le cadeau.
            new() { Name = "tutorial_1", Make = a => a.Screens.Tutorial(a, true), After = a => Run(0.9f) },
            new() { Name = "tutorial_1_drag", Make = a => a.Screens.Tutorial(a, true), After = a => Hold(a, (1, 0)) },
            new() { Name = "tutorial_2", Make = a => a.Screens.Tutorial(a, true), After = a => { Tuto(a).Trace((1, 0), (1, 1)); Run(0.8f); } },
            new() { Name = "tutorial_2_trace", Make = a => a.Screens.Tutorial(a, true),
                After = a => { Tuto(a).Trace((1, 0), (1, 1)); Run(0.8f); Hold(a, TenGo.Core.TutorialLevel.Step1Demo); } },
            new() { Name = "tutorial_3", Make = a => a.Screens.Tutorial(a, true),
                After = a => { Tuto(a).Trace((1, 0), (1, 1)); Run(0.8f); Tuto(a).Trace(TenGo.Core.TutorialLevel.Step1Demo); Run(0.5f); } },
            new() { Name = "tutorial_end", Make = a => a.Screens.Tutorial(a, true), Seed = s => s.Remove(SaveKeys.WelcomeGiftPaid),
                After = a => { var t = Tuto(a); t.Trace((1, 0), (1, 1)); Run(0.8f); t.Trace(TenGo.Core.TutorialLevel.Step1Demo); Run(1f); t.Trace((2, 0), (2, 1)); Run(1f); } },
            new() { Name = "tutorial_replay_end", Make = a => a.Screens.Tutorial(a, false), Seed = s => s.SetBool(SaveKeys.WelcomeGiftPaid, true),
                After = a => { TapNamed(a, "skip"); Run(1f); } },
        };

        public static void Screens()
        {
            var langs = Arg("-langs", "fr,en,ja");
            var themes = Arg("-themes", "default,night");
            var only = Arg("-only", "");
            int count = 0;
            try
            {
                foreach (var lang in langs)
                foreach (var theme in themes)
                foreach (var shot in ScreenShots)
                {
                    if (only.Length > 0 && !only.Contains(shot.Name)) continue;
                    try
                    {
                        var path = CaptureScreen(shot, lang, theme);
                        Log(path);
                        count++;
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"[Capture] {lang}/{theme}/{shot.Name} : {e}");
                    }
                }
            }
            finally
            {
                TeardownShot();
                ProjectSetup.ClearDynamicFontData();
            }
            Debug.Log($"[Capture] {count} capture(s) d'écrans");
        }

        internal static string[] Arg(string name, string fallback)
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, name);
            var raw = i >= 0 && i + 1 < args.Length ? args[i + 1] : fallback;
            return raw.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToArray();
        }

        public static string CaptureScreen(ScreenShot shot, string lang, string theme)
        {
            var app = BootShowcase(lang, theme, shot.Seed);
            app.Show(shot.Make(app), fade: false);
            Run(0.6f);
            shot.After?.Invoke(app);
            Run(shot.Settle);
            return Shot(app.Camera, $"{ScreensDir}/{lang}_{theme}_{shot.Name}.png", W, H);
        }

        /// Application neuve, sauvegarde de vitrine, iPhone 17 Pro Max, horloge fixe.
        public static AppRoot BootShowcase(string lang, string theme, Action<ISaveStore> seed = null, SceneGeometry? geometry = null)
        {
            TeardownShot();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            FxTime.Reset();
            JuiceSettings.Reset();
            JuiceSettings.ForceFull = true;
            var store = new MemorySaveStore();
            SeedShowcase(store);
            store.SetString(SaveKeys.ActiveTheme, theme);
            if (theme != Theme.DefaultId) store.SetStringList(SaveKeys.OwnedThemes, new[] { Theme.DefaultId, theme });
            seed?.Invoke(store);
            _shotApp = AppRoot.Create(new AppRoot.Options
            {
                Store = store,
                Clock = new FixedClock(new DateTimeOffset(2026, 11, 9, 10, 0, 0, TimeSpan.FromHours(1)), TimeZoneInfo.Utc),
                Geometry = geometry ?? SceneGeometry.IPhone17ProMax,
                AutoTick = false,
                ReadDevices = false,
                Language = lang,
                ShowMenuOnBoot = false,
                Fades = false,
                StartServices = false,
                Services = new AppServices.Overrides
                {
                    Analytics = new LogAnalytics { Echo = false },
                    Interstitials = new SimulatedInterstitialAds { Ready = false },
                },
            });
            return _shotApp;
        }

        internal static void TeardownShot()
        {
            if (_shotApp != null) UnityEngine.Object.DestroyImmediate(_shotApp.gameObject);
            _shotApp = null;
            JuiceSettings.Reset();
            Look.Reset();
            FxTime.Reset();
        }

        /// `GameViewController.seedShowcaseData` : joueur crédible de niveau ~20.
        public static void SeedShowcase(ISaveStore d)
        {
            d.SetInt(SaveKeys.TotalXP, 4820);
            d.SetInt(SaveKeys.CoinsBalance, 340);
            d.SetInt(SaveKeys.StreakCurrent, 12);
            d.SetInt(SaveKeys.StreakBest, 18);
            d.SetInt(SaveKeys.StreakShieldCount, 1);
            d.SetInt(SaveKeys.StatsBestChain, 9);
            d.SetInt(SaveKeys.StatsPerfectTotal, 7);
            d.SetInt(SaveKeys.StatsTotalChains, 1463);
            d.SetInt(SaveKeys.StatsTotalRushGames, 24);
            d.SetInt(SaveKeys.StatsTotalDaily, 31);
            d.SetIntList(SaveKeys.HighScores, new[] { 4820, 4310, 3980, 3720, 3450 });
            d.SetIntList(SaveKeys.RushHighScores, new[] { 2140, 1980, 1760 });
            d.SetBool(SaveKeys.HasSeenTutorial, true);
            d.SetBool(SaveKeys.NoAdsPurchased, true);
            d.SetBool(SaveKeys.NotifRequested, true);
            d.SetInt(SaveKeys.NotifGamesPlayed, 40);
            d.SetBool(SaveKeys.HasSeenShopPurchaseGuide, true);
            foreach (var b in Boosters.All)
            {
                d.SetBool(SaveKeys.BoosterCoachSeen(b.Id()), true);
                d.SetInt(SaveKeys.BoosterInventory(b.Id()), 2);
            }
            // Puzzles : les sept premiers niveaux résolus, étoiles variées.
            int[] stars = { 3, 3, 2, 3, 1, 2, 3 };
            for (int i = 0; i < stars.Length; i++) d.SetInt(SaveKeys.PuzzleStars(1, i + 1), stars[i]);
            d.SetStringList(SaveKeys.DuelHistory, new[] { "K7MQ2P", "B4XN9T", "R2WH6C" });
        }

        internal static void SeedResumeAndBloom(ISaveStore s)
        {
            var clock = new FixedClock(new DateTimeOffset(2026, 11, 9, 10, 0, 0, TimeSpan.FromHours(1)), TimeZoneInfo.Utc);
            new SeedRepository(s, clock).ForceBloomForQa(6);
            var grid = GridModel.Generate(new SeededGenerator(42));
            new GameStateRepository(s).Save(grid, 1240);
        }

        /// Graine qui pousse encore : la pastille affiche un temps (« 1h 13min »), plus large que « ×3 ».
        static void SeedResumeAndGrowing(ISaveStore s)
        {
            var clock = new FixedClock(DateTimeOffset.Now, TimeZoneInfo.Utc);
            new SeedRepository(s, clock).Offer(6);
            var grid = GridModel.Generate(new SeededGenerator(42));
            new GameStateRepository(s).Save(grid, 40);
        }

        internal static void SeedMirror(ISaveStore s)
        {
            var clock = new FixedClock(new DateTimeOffset(2026, 11, 9, 10, 0, 0, TimeSpan.FromHours(1)), TimeZoneInfo.Utc);
            new MirrorRepository(s, clock).SeedForQa();
        }

        internal static void Run(float seconds)
        {
            const float dt = 1f / 60f;
            int n = Mathf.Max(1, Mathf.RoundToInt(seconds / dt));
            for (int i = 0; i < n; i++)
            {
                FxTime.Step(dt);
                _shotApp.Tick(dt);
            }
        }

        static TutorialScreen Tuto(AppRoot app) => (TutorialScreen)app.Current;

        /// Doigt posé : appuie sur la première case puis glisse sur les suivantes, sans relâcher.
        static void Hold(AppRoot app, params (int, int)[] cells)
        {
            var t = Tuto(app);
            t.PointerDown(t.BubblePoint(cells[0].Item1, cells[0].Item2).Value);
            for (int i = 1; i < cells.Length; i++) t.PointerMove(t.BubblePoint(cells[i].Item1, cells[i].Item2).Value);
            Run(0.3f);
        }

        static void TapNamed(AppRoot app, string name)
        {
            if (app.Current is ScreenBase s && s.TargetPoint(name) is Vector2 p)
            {
                app.Input.InjectDown(p);
                Run(1f / 60f);
                app.Input.InjectUp(p);
                Run(1f / 60f);
            }
            else Debug.LogWarning($"[Capture] cible « {name} » introuvable");
        }
    }
}
