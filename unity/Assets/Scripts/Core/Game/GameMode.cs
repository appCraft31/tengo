#nullable enable
using System;
using System.Globalization;

namespace TenGo.Core
{
    /// Mode de partie avec ses paramètres. Encodé en chaîne (navigation,
    /// liens, sauvegarde) comme sur Android : `normal`, `daily`, `demo`, `rush`,
    /// `puzzle:<monde>:<index>`, `duel:<graine>:<code|->`.
    public readonly struct GameMode : IEquatable<GameMode>
    {
        private GameMode(GameModeKind kind, int world = 0, int index = 0, long seed = 0, string? code = null)
        {
            Kind = kind;
            World = world;
            Index = index;
            Seed = seed;
            Code = code;
        }

        public GameModeKind Kind { get; }
        /// Puzzle : monde et index du niveau.
        public int World { get; }
        public int Index { get; }
        /// Duel : graine de la grille (UInt64 Swift réinterprété en signé).
        public long Seed { get; }
        /// Duel : code du duel rejoint ; `null` pour le challenger (duel pas encore créé).
        public string? Code { get; }

        public static readonly GameMode Normal = new GameMode(GameModeKind.Normal);
        public static readonly GameMode Daily = new GameMode(GameModeKind.Daily);
        public static readonly GameMode Demo = new GameMode(GameModeKind.Demo);
        public static readonly GameMode Rush = new GameMode(GameModeKind.Rush);

        public static GameMode Puzzle(int world, int index) => new GameMode(GameModeKind.Puzzle, world, index);

        public static GameMode Duel(long seed, string? code) => new GameMode(GameModeKind.Duel, seed: seed, code: code);

        /// Nom du mode dans les événements analytics (`level_end`, `first_game_*`).
        public string AnalyticsName => Kind switch
        {
            GameModeKind.Normal => "normal",
            GameModeKind.Daily => "daily",
            GameModeKind.Demo => "demo",
            GameModeKind.Rush => "rush",
            GameModeKind.Puzzle => "puzzle",
            _ => "duel",
        };

        public string Encode() => Kind switch
        {
            GameModeKind.Puzzle => "puzzle:" + World.ToString(CultureInfo.InvariantCulture) + ":" + Index.ToString(CultureInfo.InvariantCulture),
            GameModeKind.Duel => "duel:" + Seed.ToString(CultureInfo.InvariantCulture) + ":" + (Code ?? "-"),
            _ => AnalyticsName,
        };

        /// Décodage tolérant : toute chaîne inconnue donne le mode normal.
        public static GameMode Decode(string? raw)
        {
            string[] parts = (raw ?? "normal").Split(':');
            string? Part(int i) => i < parts.Length ? parts[i] : null;
            int IntOr(string? s, int fallback) => int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? v : fallback;
            switch (parts[0])
            {
                case "daily": return Daily;
                case "demo": return Demo;
                case "rush": return Rush;
                case "puzzle": return Puzzle(IntOr(Part(1), 1), IntOr(Part(2), 1));
                case "duel":
                    long seed = long.TryParse(Part(1), NumberStyles.Integer, CultureInfo.InvariantCulture, out long s) ? s : 1L;
                    string? code = Part(2);
                    return Duel(seed, string.IsNullOrEmpty(code) || code == "-" ? null : code);
                default: return Normal;
            }
        }

        public bool Equals(GameMode other) =>
            Kind == other.Kind && World == other.World && Index == other.Index && Seed == other.Seed && Code == other.Code;

        public override bool Equals(object? obj) => obj is GameMode other && Equals(other);

        public override int GetHashCode() => ((int)Kind * 397) ^ World ^ (Index << 8) ^ Seed.GetHashCode() ^ (Code?.GetHashCode() ?? 0);

        public static bool operator ==(GameMode a, GameMode b) => a.Equals(b);

        public static bool operator !=(GameMode a, GameMode b) => !a.Equals(b);

        public override string ToString() => Encode();
    }
}
