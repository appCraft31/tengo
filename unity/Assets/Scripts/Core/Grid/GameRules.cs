#nullable enable
namespace TenGo.Core
{
    /// Constantes de règles du jeu (GridModel.swift, GameScene.swift).
    public static class GameRules
    {
        public const int Rows = 9;
        public const int Cols = 7;
        public const int TargetSum = 10;
        public const int MinValue = 1;
        public const int MaxValue = 9;

        /// Bonus de victoire quand la grille est entièrement vidée (mode normal,
        /// et à chaque grille vidée en Rush). Nul en Puzzle.
        public const int WinBonus = 1000;
    }
}
