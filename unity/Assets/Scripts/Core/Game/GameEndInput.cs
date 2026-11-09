#nullable enable
namespace TenGo.Core
{
    /// Ce que la partie sait à sa fin.
    public sealed class GameEndInput
    {
        public GameEndInput(GameMode mode, int score, bool won, int combos, int longestChain, int dailyDayKey = 0,
            PuzzleLevel? puzzle = null, MirrorTally? mirrorTally = null, GameEndResult? continuedFrom = null)
        {
            Mode = mode;
            Score = score;
            Won = won;
            Combos = combos;
            LongestChain = longestChain;
            DailyDayKey = dailyDayKey;
            Puzzle = puzzle;
            MirrorTally = mirrorTally;
            ContinuedFrom = continuedFrom;
        }

        public GameMode Mode { get; }
        public int Score { get; }
        /// Grille vidée.
        public bool Won { get; }
        /// Chaînes validées dans la partie.
        public int Combos { get; }
        public int LongestChain { get; }
        /// Défi du jour : clé du jour de la grille jouée.
        public int DailyDayKey { get; }
        /// Puzzle (ou démo d'un puzzle) : le niveau joué.
        public PuzzleLevel? Puzzle { get; }
        /// Mesures du miroir relevées pendant la partie.
        public MirrorTally? MirrorTally { get; }
        /// Partie reprise après « Continuer » : le bilan déjà enregistré.
        public GameEndResult? ContinuedFrom { get; }
    }
}
