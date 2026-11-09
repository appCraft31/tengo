#nullable enable
namespace TenGo.Core
{
    /// Mesures d'une grille candidate (GridAssessment, GridValidator.swift).
    /// Structure : l'égalité compare tous les champs.
    public readonly struct GridAssessment
    {
        public GridAssessment(bool hasValidMove, int easyPairs, int shortGroups, int medianMoves,
            int worstMoves, int medianScore, int difficulty)
        {
            HasValidMove = hasValidMove;
            EasyPairs = easyPairs;
            ShortGroups = shortGroups;
            MedianMoves = medianMoves;
            WorstMoves = worstMoves;
            MedianScore = medianScore;
            Difficulty = difficulty;
        }

        /// Au moins un coup existe.
        public bool HasValidMove { get; }
        /// Paires immédiates (2 bulles).
        public int EasyPairs { get; }
        /// Groupes courts (≤ 4 bulles) : la « matière » jouable.
        public int ShortGroups { get; }
        /// Coups avant blocage, médiane des parties simulées.
        public int MedianMoves { get; }
        /// Pire des parties simulées.
        public int WorstMoves { get; }
        /// Score médian d'une partie jouée au hasard.
        public int MedianScore { get; }
        /// Difficulté 1–100.
        public int Difficulty { get; }

        /// Jouable et offrant une vraie partie.
        public bool IsPublishable =>
            HasValidMove && WorstMoves >= GridValidator.MinWorstMoves && ShortGroups >= GridValidator.MinShortGroups;

        public GridAssessment With(bool? hasValidMove = null, int? shortGroups = null, int? worstMoves = null) =>
            new GridAssessment(hasValidMove ?? HasValidMove, EasyPairs, shortGroups ?? ShortGroups, MedianMoves,
                worstMoves ?? WorstMoves, MedianScore, Difficulty);
    }
}
