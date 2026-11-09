#nullable enable
using System;

namespace TenGo.Core
{
    /// Coordonnée de case (ligne, colonne). Ligne 0 = bas de l'écran, comme iOS.
    public readonly struct Cell : IEquatable<Cell>
    {
        public Cell(int row, int col)
        {
            Row = row;
            Col = col;
        }

        public int Row { get; }
        public int Col { get; }

        /// Index linéaire `row × cols + col` (masques de bits, ensembles).
        public int Index => Row * GameRules.Cols + Col;

        public static Cell FromIndex(int index) => new Cell(index / GameRules.Cols, index % GameRules.Cols);

        public bool Equals(Cell other) => Row == other.Row && Col == other.Col;
        public override bool Equals(object? obj) => obj is Cell other && Equals(other);
        public override int GetHashCode() => Row * 31 + Col;
        public static bool operator ==(Cell a, Cell b) => a.Equals(b);
        public static bool operator !=(Cell a, Cell b) => !a.Equals(b);
        public override string ToString() => "(" + Row + "," + Col + ")";
    }
}
