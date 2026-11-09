#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace TenGo.Core
{
    /// Grille de jeu 9 × 7, portage de GridModel.swift.
    ///
    /// `Cells[row][col]` contient une bulle ou `null` (case vide). `Blocked`
    /// marque les pierres du défi du jour : une case bloquée ne contient pas de
    /// bulle, casse l'adjacence du tracé et segmente la colonne pour la gravité.
    ///
    /// Le `struct` Swift est une valeur ; ici la classe est mutable : `Copy()`
    /// fait une copie profonde quand la sémantique de valeur compte (simulations,
    /// défi du jour en cache).
    public sealed class GridModel
    {
        private GridModel(BubbleModel?[][] cells, bool[][] blocked)
        {
            Cells = cells;
            Blocked = blocked;
        }

        public BubbleModel?[][] Cells { get; }
        public bool[][] Blocked { get; }

        /// Bulle en (row, col), `null` si vide ou hors grille.
        public BubbleModel? this[int row, int col] =>
            row >= 0 && row < GameRules.Rows && col >= 0 && col < GameRules.Cols ? Cells[row][col] : null;

        public BubbleModel? this[Cell cell] => this[cell.Row, cell.Col];

        // MARK: - Fabriques

        /// Grille aléatoire jouable, reproductible depuis le générateur injecté
        /// (`GridModel(using:)`). Le mode normal passe un générateur système.
        public static GridModel Generate(IRng rng) => new GridModel(GenerateCells(rng), EmptyMask());

        /// Grille vide (tests, outils).
        public static GridModel Empty() => new GridModel(EmptyCells(), EmptyMask());

        /// Mode Puzzles : disposition fixe validée hors ligne. Format « colonnes
        /// séparées par `|`, chiffres de bas en haut » (ligne 0 = bas) ; un
        /// segment vide est une colonne vide, les caractères hors 1–9 sont ignorés.
        public static GridModel FromPuzzleLayout(string layout)
        {
            var cells = EmptyCells();
            string[] columns = layout.Split('|');
            for (int col = 0; col < columns.Length; col++)
            {
                if (col >= GameRules.Cols) break;
                string column = columns[col];
                for (int row = 0; row < column.Length; row++)
                {
                    char ch = column[row];
                    if (row >= GameRules.Rows || ch < '1' || ch > '9') continue;
                    cells[row][col] = new BubbleModel(ch - '0', row, col);
                }
            }
            return new GridModel(cells, EmptyMask());
        }

        /// Reconstruit une grille depuis une sauvegarde (masque absent = aucune pierre).
        public static GridModel FromSnapshot(BubbleModel?[][] cells, bool[][]? blocked) =>
            new GridModel(cells, blocked ?? EmptyMask());

        private static BubbleModel?[][] EmptyCells()
        {
            var cells = new BubbleModel?[GameRules.Rows][];
            for (int r = 0; r < GameRules.Rows; r++) cells[r] = new BubbleModel?[GameRules.Cols];
            return cells;
        }

        private static bool[][] EmptyMask()
        {
            var mask = new bool[GameRules.Rows][];
            for (int r = 0; r < GameRules.Rows; r++) mask[r] = new bool[GameRules.Cols];
            return mask;
        }

        private static BubbleModel?[][] GenerateCells(IRng rng)
        {
            for (int attempt = 0; attempt < 100; attempt++)
            {
                var candidate = MakeCells(rng);
                if (new GridModel(candidate, EmptyMask()).HasValidMove()) return candidate;
            }
            // Repli : force au moins une paire 5 + 5 adjacente.
            var fallback = MakeCells(rng);
            fallback[0][0] = new BubbleModel(5, 0, 0);
            fallback[0][1] = new BubbleModel(5, 0, 1);
            return fallback;
        }

        /// Ordre des tirages : lignes puis colonnes, comme `(0..<rows).map { (0..<cols).map }`.
        private static BubbleModel?[][] MakeCells(IRng rng)
        {
            var cells = EmptyCells();
            for (int row = 0; row < GameRules.Rows; row++)
                for (int col = 0; col < GameRules.Cols; col++)
                    cells[row][col] = new BubbleModel(rng.NextInt(GameRules.MinValue, GameRules.MaxValue), row, col);
            return cells;
        }

        private static bool InBounds(int row, int col) =>
            row >= 0 && row < GameRules.Rows && col >= 0 && col < GameRules.Cols;

        // MARK: - Twists du défi du jour

        /// Positions occupées par une bulle, lignes puis colonnes.
        public List<Cell> BubblePositions()
        {
            var positions = new List<Cell>();
            for (int row = 0; row < GameRules.Rows; row++)
                for (int col = 0; col < GameRules.Cols; col++)
                    if (Cells[row][col] != null) positions.Add(new Cell(row, col));
            return positions;
        }

        /// Twist « obstacle » : transforme `count` cases en pierres inertes.
        public void PlaceObstacles(int count, IRng rng)
        {
            foreach (Cell pos in BubblePositions().ShuffledSwift(rng).Take(count))
            {
                Cells[pos.Row][pos.Col] = null;
                Blocked[pos.Row][pos.Col] = true;
            }
        }

        /// Twist « ancrée » : `count` bulles ne tomberont pas avec la gravité.
        public void AnchorBubbles(int count, IRng rng)
        {
            foreach (Cell pos in BubblePositions().ShuffledSwift(rng).Take(count))
            {
                BubbleModel? b = Cells[pos.Row][pos.Col];
                if (b != null) b.IsAnchored = true;
            }
        }

        /// Twist « gelée » : `count` bulles sont givrées jusqu'à fonte.
        public void FreezeBubbles(int count, IRng rng)
        {
            foreach (Cell pos in BubblePositions().ShuffledSwift(rng).Take(count))
            {
                BubbleModel? b = Cells[pos.Row][pos.Col];
                if (b != null) b.IsFrozen = true;
            }
        }

        // MARK: - Fleur de dix

        /// Pose une fleur de dix sur une bulle ordinaire, de préférence au cœur
        /// de la grille et sur un petit chiffre (1 à 4, qui entre dans une longue
        /// chaîne). Renvoie sa position, ou `null` si aucune bulle ne convient.
        public Cell? PlantFlower(int power, IRng rng)
        {
            List<Cell> candidates = BubblePositions().Where(pos =>
            {
                BubbleModel? b = Cells[pos.Row][pos.Col];
                return b != null && !b.IsAnchored && !b.IsFrozen && b.Bloom == 0;
            }).ToList();
            List<Cell> central = candidates.Where(p => p.Row >= 2 && p.Row <= 6 && p.Col >= 1 && p.Col <= 5).ToList();
            List<Cell> Small(List<Cell> list) => list.Where(p => (Cells[p.Row][p.Col]?.Value ?? 10) <= 4).ToList();
            List<Cell> pool = new[] { Small(central), Small(candidates), central, candidates }
                .FirstOrDefault(l => l.Count > 0) ?? new List<Cell>();
            if (!pool.TryRandomElementSwift(rng, out Cell pick)) return null;
            Cells[pick.Row][pick.Col]!.Bloom = power;
            return pick;
        }

        /// Capture de fiche : pose une fleur ×N sur une case précise.
        public void SetBloom(int power, Cell at)
        {
            BubbleModel? b = Cells[at.Row][at.Col];
            if (b != null) b.Bloom = power;
        }

        /// Multiplicateur porté par le chemin (le plus fort s'il y a plusieurs fleurs).
        public int BloomMultiplier(IEnumerable<Cell> path)
        {
            int best = 1;
            bool any = false;
            foreach (Cell c in path)
            {
                int bloom = Cells[c.Row][c.Col]?.Bloom ?? 0;
                if (bloom > 1 && (!any || bloom > best))
                {
                    best = bloom;
                    any = true;
                }
            }
            return best;
        }

        // MARK: - Requêtes

        public static bool IsAdjacent(Cell a, Cell b)
        {
            int dr = Math.Abs(a.Row - b.Row);
            int dc = Math.Abs(a.Col - b.Col);
            return dr <= 1 && dc <= 1 && (dr != 0 || dc != 0);
        }

        public int PathSum(IEnumerable<Cell> path) => path.Sum(c => Cells[c.Row][c.Col]?.Value ?? 0);

        /// « Vide » = plus aucune bulle ; les pierres ne comptent pas.
        public bool IsGridEmpty() => Cells.All(row => row.All(b => b == null));

        /// Critère du défi du jour : au moins un coup jouable existe.
        public bool IsSolvable() => HasValidMove();

        /// Nombre de groupes DISTINCTS (≤ maxLen) formant un chemin adjacent de somme 10.
        public int CountSumTenGroups(int maxLen) => SumTenGroups(maxLen).Count;

        /// Les groupes de somme 10 eux-mêmes, dédupliqués par ensemble de cases
        /// (masque 64 bits, 63 cases). Sert à simuler des parties (GridValidator).
        /// L'ordre d'énumération (dr externe, dc interne, lignes puis colonnes)
        /// est celui d'iOS : le tirage du validateur y indexe, il conditionne donc
        /// la parité du défi du jour.
        public List<List<Cell>> SumTenGroups(int maxLen)
        {
            var seen = new HashSet<ulong>();
            var groups = new List<List<Cell>>();
            var path = new List<Cell>();

            void Explore(int row, int col, int sum, int depth, ulong mask)
            {
                if (sum == GameRules.TargetSum)
                {
                    if (seen.Add(mask)) groups.Add(new List<Cell>(path));
                    return;
                }
                if (sum > GameRules.TargetSum || depth >= maxLen) return;
                for (int dr = -1; dr <= 1; dr++)
                {
                    for (int dc = -1; dc <= 1; dc++)
                    {
                        if (dr == 0 && dc == 0) continue;
                        int nr = row + dr, nc = col + dc;
                        if (!InBounds(nr, nc)) continue;
                        BubbleModel? nb = Cells[nr][nc];
                        if (nb == null || nb.IsFrozen) continue;
                        ulong bit = 1UL << (nr * GameRules.Cols + nc);
                        if ((mask & bit) != 0) continue;
                        path.Add(new Cell(nr, nc));
                        Explore(nr, nc, sum + nb.Value, depth + 1, mask | bit);
                        path.RemoveAt(path.Count - 1);
                    }
                }
            }

            for (int row = 0; row < GameRules.Rows; row++)
            {
                for (int col = 0; col < GameRules.Cols; col++)
                {
                    BubbleModel? b = Cells[row][col];
                    if (b == null || b.IsFrozen) continue;
                    ulong bit = 1UL << (row * GameRules.Cols + col);
                    path.Clear();
                    path.Add(new Cell(row, col));
                    Explore(row, col, b.Value, 1, bit);
                }
            }
            return groups;
        }

        /// Existe-t-il un chemin de somme exactement 10 ? (bulles gelées intraversables)
        public bool HasValidMove()
        {
            var visited = new bool[GameRules.Rows * GameRules.Cols];
            for (int row = 0; row < GameRules.Rows; row++)
            {
                for (int col = 0; col < GameRules.Cols; col++)
                {
                    BubbleModel? bubble = Cells[row][col];
                    if (bubble == null || bubble.IsFrozen) continue;
                    Array.Clear(visited, 0, visited.Length);
                    visited[row * GameRules.Cols + col] = true;
                    if (Dfs(row, col, bubble.Value, visited)) return true;
                }
            }
            return false;
        }

        private bool Dfs(int row, int col, int sum, bool[] visited)
        {
            if (sum == GameRules.TargetSum) return true;
            if (sum > GameRules.TargetSum) return false;
            for (int dr = -1; dr <= 1; dr++)
            {
                for (int dc = -1; dc <= 1; dc++)
                {
                    if (dr == 0 && dc == 0) continue;
                    int nr = row + dr, nc = col + dc;
                    if (!InBounds(nr, nc)) continue;
                    BubbleModel? neighbor = Cells[nr][nc];
                    if (neighbor == null || neighbor.IsFrozen) continue;
                    int key = nr * GameRules.Cols + nc;
                    if (visited[key]) continue;
                    visited[key] = true;
                    if (Dfs(nr, nc, sum + neighbor.Value, visited)) return true;
                    visited[key] = false;
                }
            }
            return false;
        }

        // MARK: - Mutations

        /// Booster « Mélange » : réattribue une valeur aléatoire (1–9) à chaque
        /// bulle, en conservant positions, pierres, ancrages, givre et fleurs.
        /// Réessaie jusqu'à obtenir une grille jouable, avec repli garanti.
        public void ReshuffleValues(IRng rng)
        {
            List<Cell> positions = BubblePositions();

            void SetValue(int value, Cell pos)
            {
                BubbleModel? old = Cells[pos.Row][pos.Col];
                if (old != null) Cells[pos.Row][pos.Col] = old.WithValue(value);
            }

            for (int attempt = 0; attempt < 100; attempt++)
            {
                foreach (Cell pos in positions) SetValue(rng.NextInt(GameRules.MinValue, GameRules.MaxValue), pos);
                if (HasValidMove()) return;
            }
            // Repli : force une paire 5 + 5 adjacente parmi les bulles existantes.
            int firstIndex = positions.FindIndex(p => positions.Any(q => IsAdjacent(p, q)));
            if (firstIndex < 0) return;
            Cell first = positions[firstIndex];
            Cell second = positions.First(q => IsAdjacent(first, q));
            SetValue(5, first);
            SetValue(5, second);
        }

        public void RemoveBubbles(IEnumerable<Cell> path)
        {
            foreach (Cell pos in path) Cells[pos.Row][pos.Col] = null;
        }

        /// Fait fondre le givre des bulles gelées adjacentes au chemin validé.
        /// Renvoie les positions dégelées (pour l'animation).
        public List<Cell> ThawFrozenBubbles(IEnumerable<Cell> path)
        {
            var thawed = new List<Cell>();
            var seen = new HashSet<Cell>();
            foreach (Cell step in path)
            {
                for (int dr = -1; dr <= 1; dr++)
                {
                    for (int dc = -1; dc <= 1; dc++)
                    {
                        if (dr == 0 && dc == 0) continue;
                        int nr = step.Row + dr, nc = step.Col + dc;
                        if (!InBounds(nr, nc)) continue;
                        var pos = new Cell(nr, nc);
                        if (!seen.Add(pos)) continue;
                        BubbleModel? b = Cells[nr][nc];
                        if (b != null && b.IsFrozen)
                        {
                            b.IsFrozen = false;
                            thawed.Add(pos);
                        }
                    }
                }
            }
            return thawed;
        }

        /// Compacte chaque colonne vers le bas (ligne 0) ; renvoie les mouvements.
        /// Les bulles ancrées restent en place et bornent les segments ; les
        /// pierres segmentent la colonne.
        public List<GravityMove> ApplyGravity()
        {
            var movements = new List<GravityMove>();
            for (int col = 0; col < GameRules.Cols; col++)
            {
                var newCol = new BubbleModel?[GameRules.Rows];
                for (int row = 0; row < GameRules.Rows; row++)
                {
                    BubbleModel? b = Cells[row][col];
                    if (b != null && b.IsAnchored) newCol[row] = b;
                }

                bool IsBarrier(int r) => Blocked[r][col] || (Cells[r][col]?.IsAnchored ?? false);

                int current = GameRules.Rows - 1;
                while (current >= 0)
                {
                    if (IsBarrier(current))
                    {
                        current--;
                        continue;
                    }
                    int top = current;
                    while (top - 1 >= 0 && !IsBarrier(top - 1)) top--;
                    int bottom = current;

                    var falling = new List<BubbleModel>();
                    for (int r = top; r <= bottom; r++)
                    {
                        BubbleModel? b = Cells[r][col];
                        if (b != null && !b.IsAnchored) falling.Add(b);
                    }
                    for (int i = 0; i < falling.Count; i++)
                    {
                        BubbleModel model = falling[i];
                        int toRow = top + i;
                        int fromRow = model.Row;
                        model.Row = toRow;
                        newCol[toRow] = model;
                        if (fromRow != toRow) movements.Add(new GravityMove(model, fromRow, toRow, col));
                    }
                    current = top - 1;
                }

                for (int r = 0; r < GameRules.Rows; r++) Cells[r][col] = newCol[r];
            }
            return movements;
        }

        /// Copie profonde (même identité de bulles, état indépendant).
        public GridModel Copy()
        {
            var cells = EmptyCells();
            var blocked = EmptyMask();
            for (int r = 0; r < GameRules.Rows; r++)
            {
                for (int c = 0; c < GameRules.Cols; c++)
                {
                    cells[r][c] = Cells[r][c]?.Clone();
                    blocked[r][c] = Blocked[r][c];
                }
            }
            return new GridModel(cells, blocked);
        }
    }
}
