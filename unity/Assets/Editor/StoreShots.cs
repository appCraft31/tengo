using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TenGo.App;
using TenGo.Core;
using TenGo.Game;
using UnityEngine;

namespace TenGo.Editor
{
    /// Captures de fiche store en haute définition, dans les 10 langues du jeu,
    /// sur téléphone (iPhone 17 Pro Max, 1320 × 2868) et tablette (Android 10",
    /// 1600 × 2560), thème par défaut, sans pub ni fenêtre : Logs/store/<langue>/<appareil>/<id>.png
    /// et Logs/store/index.json.
    ///   Tools/unity.sh TenGo.Editor.StoreShots.Run [-langs fr,en] [-devices phone,tablet] [-only 01_chain,07_shop]
    /// Réutilise la vitrine de `Captures.Screens` (`BootShowcase`, `SeedShowcase`, `SeedMirror`).
    public static class StoreShots
    {
        public const string Dir = "Logs/store";

        /// Codes du catalogue `strings.json` (dossiers de sortie identiques).
        public static readonly string[] Languages = { "fr", "en", "de", "es", "it", "ja", "ko", "nl", "pt-BR", "zh-Hans" };

        sealed class Device
        {
            public string Id;
            public SceneGeometry Geometry;
        }

        static readonly Device[] Devices =
        {
            new() { Id = "phone", Geometry = SceneGeometry.IPhone17ProMax },
            new() { Id = "tablet", Geometry = SceneGeometry.AndroidTablet10 },
        };

        sealed class Shot
        {
            public string Id;
            public Action<ISaveStore> Seed;
            public Action<AppRoot> Stage;
        }

        static AppRoot _app;
        const float Dt = 1f / 60f;

        static readonly Shot[] Shots =
        {
            // 1. Partie Normal en cours, chemin de 5 bulles tracé, score 1 240.
            new() { Id = "01_chain", Stage = a =>
            {
                StartGame(a, GameLaunch.Normal(new SavedGame(GridModel.Generate(new SeededGenerator(42)), 1240)));
                a.Game.QaShowcasePath(5, 1240);
                Captures.Run(0.15f);
            } },
            // 2. Fleur ×3 éclose au cœur de la grille (posée comme celle d'une graine récoltée).
            new() { Id = "02_flower", Stage = a =>
            {
                var grid = GridModel.Generate(new SeededGenerator(7));
                grid.PlantFlower(3, new SeededGenerator(3));
                StartGame(a, GameLaunch.Normal(new SavedGame(grid, 1860)));
                Captures.Run(0.6f);
            } },
            // 3. Ton cerveau, rempli.
            new() { Id = "03_brain", Seed = Captures.SeedMirror, Stage = a => ShowScreen(a, a.Screens.Mirror(a)) },
            // 4. Fin de partie : nouveau record et « Nouveau 10 trouvé ».
            new() { Id = "04_end", Seed = s =>
            {
                Captures.SeedMirror(s);
                s.SetIntList(SaveKeys.HighScores, new[] { 4310, 3980, 3720, 3450, 3100 });
            }, Stage = EndWithRecord },
            // 5. Défi du jour (pierres) en cours : un coup joué, le suivant en cours de tracé.
            new() { Id = "05_daily", Stage = a =>
            {
                StartGame(a, GameLaunch.ForDaily());
                PlayOne(a, 4);
                // Laisse s'éteindre l'annonce des succès débloqués par le premier coup.
                Captures.Run(3.5f);
                a.Game.QaShowcasePath(5);
                Captures.Run(0.15f);
            } },
            // 6. Accueil sans reprise.
            new() { Id = "06_menu", Stage = a => ShowScreen(a, a.Screens.Menu(a)) },
            // 7. Boutique, haut de page.
            new() { Id = "07_shop", Stage = a => ShowScreen(a, a.Screens.Shop(a, false)) },
        };

        public static void Run()
        {
            var langs = Captures.Arg("-langs", string.Join(",", Languages));
            var devices = Captures.Arg("-devices", "phone,tablet");
            var only = Captures.Arg("-only", "");
            var index = new List<(string lang, string device, string id, string path, int w, int h)>();
            int failures = 0;
            try
            {
                foreach (var lang in langs)
                foreach (var device in Devices.Where(d => devices.Contains(d.Id)))
                foreach (var shot in Shots)
                {
                    if (only.Length > 0 && !only.Contains(shot.Id)) continue;
                    try
                    {
                        var rel = $"{lang}/{device.Id}/{shot.Id}.png";
                        var g = device.Geometry;
                        _app = Captures.BootShowcase(lang, Theme.DefaultId, s => { SeedStore(s); shot.Seed?.Invoke(s); }, g);
                        shot.Stage(_app);
                        var path = Captures.ShotTo(_app.Camera, Path.Combine(Dir, rel), g.PixelWidth, g.PixelHeight);
                        Debug.Log($"[Store] {path}");
                        index.Add((lang, device.Id, shot.Id, rel, g.PixelWidth, g.PixelHeight));
                    }
                    catch (Exception e)
                    {
                        failures++;
                        Debug.LogError($"[Store] {lang}/{device.Id}/{shot.Id} : {e}");
                    }
                }
            }
            finally
            {
                Captures.TeardownShot();
                _app = null;
                ProjectSetup.ClearDynamicFontData();
            }
            WriteIndex(index);
            Debug.Log($"[Store] {index.Count} capture(s), {failures} échec(s)");
            if (failures > 0) throw new Exception($"{failures} capture(s) de fiche en échec");
        }

        /// Vitrine d'iOS, poussée un peu : niveau 25 entamé, 340 pièces, série de 12, sans pub.
        static void SeedStore(ISaveStore s)
        {
            s.SetInt(SaveKeys.TotalXP, 4950);
            s.SetInt(SaveKeys.CoinsBalance, 340);
            s.SetInt(SaveKeys.StreakCurrent, 12);
            s.SetBool(SaveKeys.NoAdsPurchased, true);
        }

        static void ShowScreen(AppRoot a, IAppScreen screen)
        {
            a.Show(screen, fade: false);
            Captures.Run(1.8f);
        }

        static void StartGame(AppRoot a, GameLaunch launch)
        {
            a.Show(a.CreateGame(launch), fade: false);
            Captures.Run(1.0f);
        }

        /// Trois coups joués, puis fin forcée sur un record : les décompositions jouées
        /// sont retirées du carnet pour que la fin annonce un « Nouveau 10 trouvé ».
        static void EndWithRecord(AppRoot a)
        {
            StartGame(a, GameLaunch.Normal(new SavedGame(GridModel.Generate(new SeededGenerator(42)), 3600)));
            var played = new HashSet<string>();
            for (int i = 0; i < 3; i++)
            {
                var key = PlayOne(a, 5);
                if (key != null) played.Add(key);
            }
            var mirror = a.Services.Mirror;
            var data = mirror.Data;
            long now = a.Services.Clock.Now.ToUnixTimeMilliseconds();
            var discovered = Partition.AllKeys.Where(k => !played.Contains(k)).Take(17)
                .Select((k, i) => (k, i)).ToDictionary(p => p.k, p => now - (20 - p.i) * 86_400_000L);
            mirror.Save(new MirrorData(data.Records, discovered));
            a.Game.QaForceGameOver(4960, 9, 41, allowSecondChance: false);
            Captures.Run(2.4f);
        }

        /// Joue le meilleur coup « vitrine » comme un doigt ; renvoie sa décomposition.
        static string PlayOne(AppRoot a, int maxLen)
        {
            WaitIdle(a);
            var game = a.Game;
            if (game == null || game.IsGameOver) return null;
            var path = game.Grid.ShowcasePath(maxLen);
            if (path == null || path.Count < 2) return null;
            var key = Partition.Key(path.Select(c => game.Grid[c]?.Value ?? 0));
            var from = game.CellCenter(path[0]);
            a.Input.InjectDown(from);
            Captures.Run(Dt);
            for (int i = 1; i < path.Count; i++)
            {
                var to = game.CellCenter(path[i]);
                for (int k = 1; k <= 4; k++)
                {
                    a.Input.InjectMove(Vector2.Lerp(from, to, k / 4f));
                    Captures.Run(Dt);
                }
                from = to;
            }
            a.Input.InjectUp(from);
            Captures.Run(0.3f);
            WaitIdle(a);
            return key;
        }

        static void WaitIdle(AppRoot a, float max = 6f)
        {
            float t = 0;
            while (t < max && a.Game != null && a.Game.Animating && !a.Game.IsGameOver) { Captures.Run(Dt); t += Dt; }
        }

        static void WriteIndex(List<(string lang, string device, string id, string path, int w, int h)> index)
        {
            var sb = new StringBuilder();
            sb.Append("{\n  \"generated\": \"").Append(DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")).Append("\",\n  \"shots\": [\n");
            for (int i = 0; i < index.Count; i++)
            {
                var e = index[i];
                sb.Append($"    {{ \"lang\": \"{e.lang}\", \"device\": \"{e.device}\", \"id\": \"{e.id}\", \"path\": \"{e.path}\", \"width\": {e.w}, \"height\": {e.h} }}");
                sb.Append(i + 1 < index.Count ? ",\n" : "\n");
            }
            sb.Append("  ]\n}\n");
            Directory.CreateDirectory(Dir);
            File.WriteAllText(Path.Combine(Dir, "index.json"), sb.ToString());
        }
    }
}
