#nullable enable
namespace TenGo.Core
{
    /// Niveau du mode Puzzles, portage de PuzzleLevel.swift. Les étoiles portent
    /// sur le SCORE : chaque coup retire exactement 10 points de valeur, un
    /// « par » en coups ne départagerait rien ; le barème superlinéaire
    /// récompense l'élégance du découpage. Pas de bonus de victoire en Puzzle.
    public sealed class PuzzleLevel
    {
        public PuzzleLevel(int index, int world, string layout, int moves, int twoStarScore, int threeStarScore)
        {
            Index = index;
            World = world;
            Layout = layout;
            Moves = moves;
            TwoStarScore = twoStarScore;
            ThreeStarScore = threeStarScore;
        }

        public int Index { get; }
        public int World { get; }
        /// Colonnes séparées par `|`, chiffres de bas en haut (ligne 0 = bas).
        public string Layout { get; }
        /// Coups nécessaires pour vider la grille (invariant : somme / 10).
        public int Moves { get; }
        public int TwoStarScore { get; }
        public int ThreeStarScore { get; }

        /// Étoiles obtenues pour un score, la grille ayant été vidée.
        public int Stars(int score)
        {
            if (score >= ThreeStarScore) return 3;
            if (score >= TwoStarScore) return 2;
            return 1;
        }

        /// Grille du niveau.
        public GridModel MakeGrid() => GridModel.FromPuzzleLayout(Layout);
    }
}
