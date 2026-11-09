#nullable enable
using System.Collections.Generic;
using System.Linq;

namespace TenGo.Core
{
    /// Solveur de chemins « somme = 10 », portage de PathSolver.swift.
    public static class PathSolver
    {
        /// Heuristique « show » : renvoie le chemin le plus LONG possible
        /// (≤ `maxLen`), qui fait le plus gros combo. À longueur égale, l'ordre
        /// de balayage tranche. Sert au booster Indice et au mode démo. `null`
        /// si aucun coup n'existe.
        public static List<Cell>? ShowcasePath(this GridModel grid, int maxLen = 5)
        {
            List<int>? best = null;
            var path = new List<int>();
            var visited = new HashSet<int>();

            void Consider()
            {
                if (best == null || path.Count > best.Count) best = new List<int>(path);
            }

            void Dfs(int row, int col, int sum)
            {
                if (sum == GameRules.TargetSum)
                {
                    Consider();
                    return;
                }
                if (sum > GameRules.TargetSum || path.Count >= maxLen) return;
                for (int dr = -1; dr <= 1; dr++)
                {
                    for (int dc = -1; dc <= 1; dc++)
                    {
                        if (dr == 0 && dc == 0) continue;
                        int nr = row + dr, nc = col + dc;
                        if (nr < 0 || nr >= GameRules.Rows || nc < 0 || nc >= GameRules.Cols) continue;
                        BubbleModel? nb = grid.Cells[nr][nc];
                        if (nb == null || nb.IsFrozen) continue;
                        int key = nr * GameRules.Cols + nc;
                        if (visited.Contains(key)) continue;
                        visited.Add(key);
                        path.Add(key);
                        Dfs(nr, nc, sum + nb.Value);
                        path.RemoveAt(path.Count - 1);
                        visited.Remove(key);
                    }
                }
            }

            for (int row = 0; row < GameRules.Rows; row++)
            {
                for (int col = 0; col < GameRules.Cols; col++)
                {
                    BubbleModel? b = grid.Cells[row][col];
                    if (b == null || b.IsFrozen) continue;
                    int key = row * GameRules.Cols + col;
                    visited.Clear();
                    visited.Add(key);
                    path.Clear();
                    path.Add(key);
                    Dfs(row, col, b.Value);
                }
            }

            return best?.Select(Cell.FromIndex).ToList();
        }
    }
}
