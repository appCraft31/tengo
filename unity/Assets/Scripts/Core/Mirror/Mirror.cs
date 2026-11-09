#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace TenGo.Core
{
    /// Miroir cognitif, logique pure (CognitiveMirror.swift) : mesure, sans
    /// rien demander, quatre aptitudes tirées des gestes du joueur et choisit
    /// la phrase de fin de partie. Les dates sont en millisecondes Unix.
    public static class Mirror
    {
        /// Parties archivées au maximum (environ deux mois de jeu régulier).
        public const int MaxRecords = 120;
        /// Coups minimum pour qu'une partie compte dans les moyennes.
        public const int MinMovesForReading = 4;

        private const long WeekMs = 7L * 86_400_000L;

        public static bool LowerIsBetter(MirrorSkill skill) => skill == MirrorSkill.Look;

        private static double? Value(MirrorSkill skill, IReadOnlyList<MirrorRecord> window, MirrorData data)
        {
            switch (skill)
            {
                case MirrorSkill.Look:
                {
                    int count = window.Sum(r => r.LookCount);
                    return count > 0 ? window.Sum(r => r.LookSum) / count : (double?)null;
                }
                case MirrorSkill.Vision:
                {
                    int moves = window.Sum(r => r.Moves);
                    return moves > 0 ? (double)window.Sum(r => r.LengthSum) / moves : (double?)null;
                }
                case MirrorSkill.Anticipation:
                {
                    int eligible = window.Sum(r => r.FallEligible);
                    return eligible > 0 ? (double)window.Sum(r => r.FallChains) / eligible : (double?)null;
                }
                default:
                    return data.Discovered.Count;
            }
        }

        private static List<T> TakeLast<T>(IEnumerable<T> source, int count)
        {
            var list = source.ToList();
            return list.Skip(Math.Max(0, list.Count - count)).ToList();
        }

        /// Lecture « cette semaine contre avant » : les parties des 7 derniers
        /// jours face aux 30 précédentes ; faute de parties récentes, les 5
        /// dernières contre le reste.
        public static MirrorReading WeeklyReading(MirrorData data, MirrorSkill skill, long nowMs)
        {
            long weekAgo = nowMs - WeekMs;
            if (skill == MirrorSkill.Repertoire)
            {
                int before = data.Discovered.Values.Count(d => d < weekAgo);
                return new MirrorReading(data.Discovered.Count, before > 0 ? before : (double?)null);
            }
            List<MirrorRecord> usable = data.Records.Where(r => r.Moves >= MinMovesForReading).ToList();
            List<MirrorRecord> recent = usable.Where(r => r.DateMs >= weekAgo).ToList();
            List<MirrorRecord> older = TakeLast(usable.Where(r => r.DateMs < weekAgo), 30);
            if (recent.Count == 0)
            {
                recent = TakeLast(usable, 5);
                older = TakeLast(usable.Take(Math.Max(0, usable.Count - 5)), 30);
            }
            return new MirrorReading(Value(skill, recent, data), older.Count == 0 ? null : Value(skill, older, data));
        }

        /// Archive la partie ; renvoie les nouvelles données et la phrase à
        /// afficher (`null` : rien à dire). Une partie sans coup n'est pas archivée.
        public static (MirrorData data, MirrorInsight? insight) FinishGame(MirrorData data, MirrorTally tally, long nowMs)
        {
            if (tally.Moves <= 0) return (data, null);
            List<MirrorRecord> history = data.Records.Where(r => r.Moves >= MinMovesForReading).ToList();

            // Nouvelles décompositions : la découverte prime sur tout le reste.
            var discovered = new Dictionary<string, long>();
            foreach (var pair in data.Discovered) discovered[pair.Key] = pair.Value;
            var fresh = new List<string>();
            foreach (string key in tally.Partitions)
            {
                if (discovered.ContainsKey(key)) continue;
                discovered[key] = nowMs;
                fresh.Add(key);
            }

            var record = new MirrorRecord(nowMs, tally.Moves, tally.LengthSum, tally.LookSum, tally.LookCount,
                tally.FallChains, tally.FallEligible);
            var records = data.Records.ToList();
            records.Add(record);
            var updated = new MirrorData(TakeLast(records, MaxRecords), discovered);

            // La plus rare (le plus de termes) ; à égalité, la première trouvée.
            string? rarest = null;
            int rarestTerms = -1;
            foreach (string key in fresh)
            {
                int terms = key.Split('+').Length;
                if (terms > rarestTerms)
                {
                    rarest = key;
                    rarestTerms = terms;
                }
            }
            if (rarest != null) return (updated, MirrorInsight.Discovered(rarest, discovered.Count));

            MirrorInsight? insight = ProgressInsight(tally, TakeLast(history, 30), updated) ?? NextTargetInsight(updated);
            return (updated, insight);
        }

        /// Meilleur progrès de la partie face à la moyenne des parties passées :
        /// au moins trois parties de référence et quatre coups joués.
        private static MirrorInsight? ProgressInsight(MirrorTally tally, IReadOnlyList<MirrorRecord> history, MirrorData data)
        {
            if (tally.Moves < MinMovesForReading || history.Count < 3) return null;
            var candidates = new List<(double gain, MirrorInsight insight)>();

            double? usualLook = Value(MirrorSkill.Look, history, data);
            if (tally.LookCount >= 3 && usualLook is double look && look > 0)
            {
                double current = tally.LookSum / tally.LookCount;
                double gain = (look - current) / look;
                if (gain >= 0.10 && look - current >= 0.2)
                    candidates.Add((gain, MirrorInsight.Faster(current, look - current)));
            }
            double? usualVision = Value(MirrorSkill.Vision, history, data);
            if (usualVision is double vision && vision > 0)
            {
                double current = (double)tally.LengthSum / tally.Moves;
                double gain = (current - vision) / vision;
                if (gain >= 0.10) candidates.Add((gain, MirrorInsight.Longer(current, vision)));
            }
            double? usualAnticipation = Value(MirrorSkill.Anticipation, history, data);
            if (tally.FallEligible >= 3 && usualAnticipation is double anticipation)
            {
                double current = (double)tally.FallChains / tally.FallEligible;
                if (current - anticipation >= 0.10)
                {
                    candidates.Add((current - anticipation, MirrorInsight.AnticipationOf(
                        (int)Math.Round(current * 100, MidpointRounding.AwayFromZero),
                        (int)Math.Round(anticipation * 100, MidpointRounding.AwayFromZero))));
                }
            }

            MirrorInsight? best = null;
            double bestGain = double.NegativeInfinity;
            foreach (var (gain, insight) in candidates)
            {
                if (best == null || gain > bestGain)
                {
                    best = insight;
                    bestGain = gain;
                }
            }
            return best;
        }

        /// À défaut de progrès : la plus petite famille de décompositions encore
        /// incomplète, comme cible pour la prochaine partie.
        private static MirrorInsight? NextTargetInsight(MirrorData data)
        {
            IReadOnlyList<int>? shortest = null;
            foreach (IReadOnlyList<int> p in Partition.All)
            {
                if (data.Discovered.ContainsKey(Partition.Key(p))) continue;
                if (shortest == null || p.Count < shortest.Count) shortest = p;
            }
            return shortest == null ? (MirrorInsight?)null
                : MirrorInsight.NextTarget(shortest.Count, data.Discovered.Count, Partition.Total);
        }
    }
}
