#nullable enable
using System.Collections.Generic;

namespace TenGo.Core
{
    /// Ce que la partie relève pour le miroir (MirrorGameTally, CognitiveMirror.swift).
    /// Remis à zéro à chaque nouvelle grille.
    public sealed class MirrorTally
    {
        /// Au-delà, le joueur fait autre chose que chercher (pause) : la mesure serait faussée.
        public const double MaxLookSeconds = 20;

        public int Moves { get; set; }
        public int LengthSum { get; set; }
        public double LookSum { get; set; }
        public int LookCount { get; set; }
        public int FallChains { get; set; }
        public int FallEligible { get; set; }

        /// Décompositions jouées dans la partie (clés canoniques), sans doublon.
        public List<string> Partitions { get; set; } = new List<string>();

        /// Une chaîne validée : ses valeurs, le temps de recherche (null si
        /// inconnu), et si elle utilise une bulle tombée au coup précédent.
        public void Record(IReadOnlyList<int> values, double? lookSeconds, bool usedFallenBubble, bool previousMoveHadFalls)
        {
            Moves += 1;
            LengthSum += values.Count;
            if (lookSeconds is double look && look >= 0 && look <= MaxLookSeconds)
            {
                LookSum += look;
                LookCount += 1;
            }
            if (previousMoveHadFalls)
            {
                FallEligible += 1;
                if (usedFallenBubble) FallChains += 1;
            }
            string key = Partition.Key(values);
            if (!Partitions.Contains(key)) Partitions.Add(key);
        }
    }
}
