using System.Collections.Generic;
using TenGo.Core;

namespace TenGo.Game
{
    /// Recherche en profondeur d'une suite de coups qui vide la grille
    /// (puzzles, petites grilles) : chaque coup retire un groupe de somme 10,
    /// dégèle les voisines puis applique la gravité, comme la partie. Sert au
    /// pilote automatique et aux tests ; `null` si rien n'est trouvé dans le budget.
    public static class BoardSolver
    {
        public static List<List<Cell>> SolveToEmpty(GridModel start, int maxLen = 9, int maxNodes = 200000)
        {
            int nodes = 0;
            var seen = new HashSet<string>();
            var moves = new List<List<Cell>>();

            bool Dfs(GridModel g)
            {
                if (g.IsGridEmpty()) return true;
                if (++nodes > maxNodes) return false;
                if (!seen.Add(Key(g))) return false;
                foreach (var group in g.SumTenGroups(maxLen))
                {
                    bool frozen = false;
                    foreach (var c in group) if (g.Cells[c.Row][c.Col]?.IsFrozen == true) frozen = true;
                    if (frozen) continue;
                    var next = g.Copy();
                    next.RemoveBubbles(group);
                    next.ThawFrozenBubbles(group);
                    next.ApplyGravity();
                    moves.Add(group);
                    if (Dfs(next)) return true;
                    moves.RemoveAt(moves.Count - 1);
                    if (nodes > maxNodes) return false;
                }
                return false;
            }

            return Dfs(start.Copy()) ? moves : null;
        }

        static string Key(GridModel g)
        {
            var chars = new char[GameRules.Rows * GameRules.Cols];
            for (int r = 0; r < GameRules.Rows; r++)
            for (int c = 0; c < GameRules.Cols; c++)
            {
                var b = g.Cells[r][c];
                chars[r * GameRules.Cols + c] = b == null ? '.' : (char)('0' + b.Value + (b.IsFrozen ? 10 : 0));
            }
            return new string(chars);
        }
    }
}
