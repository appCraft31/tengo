#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace TenGo.Core
{
    /// Défi du jour, portage de DailyChallenge.swift : une grille déterministe
    /// par jour, disponible à partir de 5 h (heure locale), sans backend. Même
    /// clé du jour ⇒ même grille sur iOS, Android et Unity.
    public static class DailyChallenge
    {
        public const int ResetHour = DayKeys.ResetHour;

        private static readonly object CacheLock = new object();
        private static DailyToday? _cached;

        /// Défi pour l'instant et le fuseau donnés.
        public static DailyToday Make(DateTimeOffset now, TimeZoneInfo zone) => Make(DayKeys.Challenge(now, zone));

        public static DailyToday Make(IClock clock) => Make(DayKeys.Challenge(clock));

        /// Défi d'une clé AAAAMMJJ. La validation coûte des dizaines de ms : la
        /// grille du jour est gardée en cache, et chaque appel reçoit une COPIE
        /// (la grille d'iOS est une valeur ; une partie ne doit pas altérer le cache).
        public static DailyToday Make(int dayKey)
        {
            DailyToday today;
            lock (CacheLock)
            {
                if (_cached == null || _cached.DayKey != dayKey)
                {
                    DailyTwist twist = DailyTwists.ForDayKey(dayKey);
                    GridModel grid = SolvableGrid(dayKey, twist, out int difficulty);
                    _cached = new DailyToday(grid, twist, dayKey, difficulty);
                }
                today = _cached;
            }
            return new DailyToday(today.Grid.Copy(), today.Twist, today.DayKey, today.Difficulty);
        }

        /// Génère 80 candidates déterministes, les VALIDE, et retient celle dont
        /// la difficulté mesurée est la plus proche de la cible du jour. Tri
        /// grossier (paires, matière) sur toutes ; simulations seulement sur 6
        /// finalistes étalés sur la plage des « paires évidentes ».
        private static GridModel SolvableGrid(int dayKey, DailyTwist twist, out int difficulty)
        {
            ulong baseSeed = unchecked((ulong)(long)dayKey);
            const int candidates = 80;
            const int finalists = 6;
            int materialFloor = GridValidator.MinShortGroups;
            int target = (int)DailyTargets.ForDay(dayKey);

            var pool = new List<(GridModel grid, int easy)>();
            GridModel? anyPlayable = null;

            for (int offset = 0; offset < candidates; offset++)
            {
                var rng = new SeededGenerator(unchecked(baseSeed + (ulong)offset));
                GridModel grid = GridModel.Generate(rng);
                Apply(twist, grid, rng);
                if (!grid.IsSolvable()) continue;
                if (anyPlayable == null) anyPlayable = grid;
                if (grid.CountSumTenGroups(4) < materialFloor) continue;
                pool.Add((grid, grid.CountSumTenGroups(2)));
            }

            // Tri STABLE (OrderBy), comme le tri de Swift : List.Sort ne l'est pas.
            var sorted = pool.OrderBy(p => p.easy).ToList();
            var picked = new List<GridModel>();
            if (sorted.Count > 0)
            {
                int step = Math.Max(1, sorted.Count / finalists);
                for (int index = 0; index < sorted.Count && picked.Count < finalists; index += step)
                    picked.Add(sorted[index].grid);
            }

            GridModel? best = null;
            int bestDifficulty = 0;
            int bestDistance = int.MaxValue;
            foreach (GridModel grid in picked)
            {
                var rng = new SeededGenerator(unchecked(baseSeed + 0xD1FFUL));
                GridAssessment assessment = GridValidator.Assess(grid, rng, 3);
                if (!assessment.IsPublishable) continue;
                int distance = Math.Abs(assessment.Difficulty - target);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = grid;
                    bestDifficulty = assessment.Difficulty;
                }
            }

            if (best != null)
            {
                difficulty = bestDifficulty;
                return best;
            }
            difficulty = target;
            // Aucune candidate validée : une grille jouable plutôt que pas de défi.
            return anyPlayable ?? GridModel.Generate(new SeededGenerator(baseSeed));
        }

        private static void Apply(DailyTwist twist, GridModel grid, IRng rng)
        {
            switch (twist)
            {
                case DailyTwist.Obstacles: grid.PlaceObstacles(twist.Count(), rng); break;
                case DailyTwist.Anchored: grid.AnchorBubbles(twist.Count(), rng); break;
                case DailyTwist.Frozen: grid.FreezeBubbles(twist.Count(), rng); break;
            }
        }
    }
}
