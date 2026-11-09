#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace TenGo.Core
{
    /// Mini niveau du tutoriel jouable (refonte 4.0, `design/refonte/07_tutoriel`),
    /// sans aléa : trois étapes sur des grilles fixes, puis la fin.
    /// - 0 « Fais 10 » : grille 3 × 3, seules les bulles 4 et 6 sont actives ;
    ///   le joueur relie 4 → 6.
    /// - 1 « Va plus loin » : grille 4 × 4 ; le joueur trace un 10 d'au moins
    ///   trois bulles (le geste montré est 4 + 2 + 1 + 3, en diagonale).
    /// - 2 « Ça tombe ! » : la chaîne validée disparaît, les bulles tombent,
    ///   de nouvelles arrivent ; le joueur trouve un 10 seul (deux bulles ou plus).
    /// - 3 : fin (« Tu as compris ! »).
    /// Lignes de haut en bas, colonnes de gauche à droite.
    public sealed class TutorialLevel
    {
        public const int StepCount = 3;

        static readonly int[,] Step0 = { { 2, 5, 8 }, { 4, 6, 1 }, { 7, 9, 3 } };
        static readonly int[,] Step1 = { { 3, 7, 2, 6 }, { 4, 2, 6, 8 }, { 1, 3, 5, 7 }, { 8, 4, 5, 9 } };
        /// Nouvelles bulles par colonne, de haut en bas, après la chute de l'étape 1.
        static readonly int[][] Refill = { new[] { 3, 6, 9 }, new[] { 5, 8, 4 }, new[] { 7, 1, 2 }, new[] { 6, 3, 5 } };

        /// Chemin montré à l'étape 1 : 4 + 2 + 1 + 3.
        public static readonly (int Row, int Col)[] Step1Demo = { (1, 0), (1, 1), (2, 0), (2, 1) };
        /// Geste montré à l'étape 0 : 4 → 6.
        public static readonly (int Row, int Col)[] Step0Demo = { (1, 0), (1, 1) };

        public int Step { get; private set; }
        public int Size => Step == 0 ? 3 : 4;
        public bool IsFinished => Step >= StepCount;
        /// Valeurs courantes (0 = case vide pendant la chute).
        public int[,] Cells { get; private set; } = (int[,])Step0.Clone();
        /// Dernière chaîne validée (pour l'animation de chute).
        public IReadOnlyList<(int Row, int Col)> LastChain { get; private set; } = Array.Empty<(int, int)>();
        /// Pour chaque case après la chute : ligne d'origine (négative = nouvelle bulle venue d'en haut).
        public int[,]? FallFrom { get; private set; }

        /// Bulle utilisable à cette étape (l'étape 0 n'active que 4 et 6).
        public bool IsActive(int row, int col) =>
            Step != 0 || Step0Demo.Contains((row, col));

        public int Value(int row, int col) => Cells[row, col];

        public static bool Adjacent((int Row, int Col) a, (int Row, int Col) b) =>
            (a.Row != b.Row || a.Col != b.Col) && Math.Abs(a.Row - b.Row) <= 1 && Math.Abs(a.Col - b.Col) <= 1;

        /// Chemin valide en cours (cases actives, voisines, sans doublon).
        public bool IsValidPath(IReadOnlyList<(int Row, int Col)> path)
        {
            if (path.Count == 0) return false;
            for (int i = 0; i < path.Count; i++)
            {
                var c = path[i];
                if (c.Row < 0 || c.Col < 0 || c.Row >= Size || c.Col >= Size || !IsActive(c.Row, c.Col)) return false;
                if (i > 0 && !Adjacent(path[i - 1], c)) return false;
                for (int j = 0; j < i; j++) if (path[j] == c) return false;
            }
            return true;
        }

        public int Sum(IReadOnlyList<(int Row, int Col)> path) => path.Sum(c => Cells[c.Row, c.Col]);

        int MinLength => Step == 1 ? 3 : 2;

        /// La chaîne fait-elle passer l'étape ?
        public bool Accepts(IReadOnlyList<(int Row, int Col)> path) =>
            !IsFinished && IsValidPath(path) && path.Count >= MinLength && Sum(path) == 10;

        /// Valide la chaîne et passe à l'étape suivante ; faux si elle est refusée.
        public bool Submit(IReadOnlyList<(int Row, int Col)> path)
        {
            if (!Accepts(path)) return false;
            LastChain = path.ToList();
            switch (Step)
            {
                case 0:
                    Cells = (int[,])Step1.Clone();
                    FallFrom = null;
                    break;
                case 1:
                    ApplyGravity(path);
                    break;
                default:
                    FallFrom = null;
                    break;
            }
            Step++;
            return true;
        }

        /// Passe directement à la fin (« Passer »).
        public void Skip() => Step = StepCount;

        void ApplyGravity(IReadOnlyList<(int Row, int Col)> removed)
        {
            int n = Size;
            var next = new int[n, n];
            var from = new int[n, n];
            for (int col = 0; col < n; col++)
            {
                var kept = new List<(int value, int row)>();
                for (int row = 0; row < n; row++)
                    if (!removed.Contains((row, col))) kept.Add((Cells[row, col], row));
                int missing = n - kept.Count;
                for (int k = 0; k < missing; k++)
                {
                    next[k, col] = Refill[col][k % Refill[col].Length];
                    from[k, col] = k - missing;
                }
                for (int k = 0; k < kept.Count; k++)
                {
                    next[missing + k, col] = kept[k].value;
                    from[missing + k, col] = kept[k].row;
                }
            }
            Cells = next;
            FallFrom = from;
        }

        /// Un 10 existe-t-il sur la grille (garde-fou de l'étape 2) ?
        public bool HasTen()
        {
            int n = Size;
            var path = new List<(int, int)>();
            bool Dfs((int Row, int Col) c, int sum)
            {
                if (sum == 10 && path.Count >= MinLength) return true;
                if (sum >= 10) return false;
                for (int dr = -1; dr <= 1; dr++)
                    for (int dc = -1; dc <= 1; dc++)
                    {
                        var d = (c.Row + dr, c.Col + dc);
                        if (d.Item1 < 0 || d.Item2 < 0 || d.Item1 >= n || d.Item2 >= n || path.Contains(d) || !IsActive(d.Item1, d.Item2)) continue;
                        path.Add(d);
                        if (Dfs(d, sum + Cells[d.Item1, d.Item2])) return true;
                        path.RemoveAt(path.Count - 1);
                    }
                return false;
            }
            for (int r = 0; r < n; r++)
                for (int c = 0; c < n; c++)
                {
                    if (!IsActive(r, c)) continue;
                    path.Clear();
                    path.Add((r, c));
                    if (Dfs((r, c), Cells[r, c])) return true;
                }
            return false;
        }
    }
}
