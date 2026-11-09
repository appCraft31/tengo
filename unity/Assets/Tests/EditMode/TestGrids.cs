using TenGo.Core;
using UnityEngine;

namespace TenGo.Tests
{
    /// Aides de construction pour les tests (TestGrids.kt, TestStore.kt).
    internal static class TestGrids
    {
        /// Grille entièrement vide.
        public static GridModel Empty() => GridModel.Empty();

        /// Grille depuis une matrice [row][col] de valeurs (0 = vide).
        public static GridModel Of(int[][] values)
        {
            GridModel g = GridModel.Empty();
            for (int r = 0; r < values.Length && r < GameRules.Rows; r++)
                for (int c = 0; c < values[r].Length && c < GameRules.Cols; c++)
                    if (values[r][c] != 0) g.Cells[r][c] = new BubbleModel(values[r][c], r, c);
            return g;
        }

        private static PuzzleCatalog _catalog;

        /// Catalogue réel, lu depuis `Resources/Puzzles/puzzles.json`.
        public static PuzzleCatalog Catalog
        {
            get
            {
                if (_catalog == null)
                {
                    TextAsset asset = Resources.Load<TextAsset>(PuzzleCatalog.ResourcePath);
                    Assert(asset != null, "Resources/Puzzles/puzzles.json introuvable");
                    _catalog = PuzzleCatalog.FromJson(asset.text);
                }
                return _catalog;
            }
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new System.InvalidOperationException(message);
        }
    }
}
