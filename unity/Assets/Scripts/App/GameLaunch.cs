using System.Collections.Generic;
using TenGo.Core;

namespace TenGo.App
{
    /// Paramètres d'une partie (initialiseurs de `GameScene`).
    public sealed class GameLaunch
    {
        public GameMode Mode = GameMode.Normal;
        /// Mode normal : partie sauvegardée à reprendre (`savedState`).
        public SavedGame Saved;
        /// Reprise après un aller-retour interne (boutique) : pas d'effets de début de session.
        public bool Resuming;
        /// Défi du jour : grille déjà calculée (sinon `DailyChallenge.Make(clock)`).
        public DailyToday Daily;
        /// Démo : graine, vitesse, longueur maximale, niveau de puzzle, coups imposés.
        public ulong DemoSeed = 7;
        public double DemoSpeed = 1.0;
        public int DemoMaxLen = 5;
        public PuzzleLevel DemoPuzzle;
        public List<List<Cell>> DemoScript;
        /// Rush : graine de la grille (null = aléatoire système, comme iOS).
        public ulong? RushSeed;

        public static GameLaunch Normal(SavedGame saved = null, bool resuming = false) =>
            new() { Mode = GameMode.Normal, Saved = saved, Resuming = resuming };
        public static GameLaunch ForDaily(DailyToday today = null) => new() { Mode = GameMode.Daily, Daily = today };
        public static GameLaunch Rush() => new() { Mode = GameMode.Rush };
        public static GameLaunch Puzzle(int world, int index) => new() { Mode = GameMode.Puzzle(world, index) };
        public static GameLaunch Duel(long seed, string code) => new() { Mode = GameMode.Duel(seed, code) };
        public static GameLaunch Demo(ulong seed, double speed = 1.0, int maxLen = 5, PuzzleLevel puzzle = null,
            List<List<Cell>> script = null) =>
            new() { Mode = GameMode.Demo, DemoSeed = seed, DemoSpeed = System.Math.Max(0.25, speed),
                    DemoMaxLen = System.Math.Min(9, System.Math.Max(2, maxLen)), DemoPuzzle = puzzle, DemoScript = script };
    }
}
