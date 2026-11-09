#nullable enable
using System;

namespace TenGo.Core
{
    /// Phrase de fin de partie du miroir (`CognitiveMirror.Insight`). Une seule
    /// phrase, la plus valorisante qui soit vraie. Les champs non concernés par
    /// la nature valent 0 (ou "").
    public readonly struct MirrorInsight : IEquatable<MirrorInsight>
    {
        private MirrorInsight(MirrorInsightKind kind, string key = "", int total = 0, double seconds = 0, double gain = 0,
            double average = 0, double usual = 0, int percent = 0, int usualPercent = 0, int length = 0, int found = 0)
        {
            Kind = kind;
            Key = key;
            Total = total;
            Seconds = seconds;
            Gain = gain;
            Average = average;
            Usual = usual;
            Percent = percent;
            UsualPercent = usualPercent;
            Length = length;
            Found = found;
        }

        public MirrorInsightKind Kind { get; }
        /// Discovered : décomposition découverte.
        public string Key { get; }
        /// Discovered, NextTarget : nombre de décompositions trouvées / total.
        public int Total { get; }
        /// Faster : temps moyen de la partie et gain face à l'habitude (s).
        public double Seconds { get; }
        public double Gain { get; }
        /// Longer : longueur moyenne de la partie et habituelle.
        public double Average { get; }
        public double Usual { get; }
        /// Anticipation : pourcentages de la partie et habituel.
        public int Percent { get; }
        public int UsualPercent { get; }
        /// NextTarget : longueur de la prochaine famille, trouvées.
        public int Length { get; }
        public int Found { get; }

        public static MirrorInsight Discovered(string key, int total) => new MirrorInsight(MirrorInsightKind.Discovered, key: key, total: total);
        public static MirrorInsight Faster(double seconds, double gain) => new MirrorInsight(MirrorInsightKind.Faster, seconds: seconds, gain: gain);
        public static MirrorInsight Longer(double average, double usual) => new MirrorInsight(MirrorInsightKind.Longer, average: average, usual: usual);
        public static MirrorInsight AnticipationOf(int percent, int usual) => new MirrorInsight(MirrorInsightKind.Anticipation, percent: percent, usualPercent: usual);
        public static MirrorInsight NextTarget(int length, int found, int total) => new MirrorInsight(MirrorInsightKind.NextTarget, length: length, found: found, total: total);

        /// Nom du paramètre analytics `kind` (identique à iOS).
        public string AnalyticsName => Kind switch
        {
            MirrorInsightKind.Discovered => "discovered",
            MirrorInsightKind.Faster => "look",
            MirrorInsightKind.Longer => "vision",
            MirrorInsightKind.Anticipation => "anticipation",
            _ => "target",
        };

        public bool Equals(MirrorInsight o) =>
            Kind == o.Kind && Key == o.Key && Total == o.Total && Seconds.Equals(o.Seconds) && Gain.Equals(o.Gain)
            && Average.Equals(o.Average) && Usual.Equals(o.Usual) && Percent == o.Percent
            && UsualPercent == o.UsualPercent && Length == o.Length && Found == o.Found;

        public override bool Equals(object? obj) => obj is MirrorInsight o && Equals(o);

        public override int GetHashCode() => ((int)Kind * 397) ^ (Key?.GetHashCode() ?? 0) ^ Total ^ Length ^ Found;

        public override string ToString() => Kind + "(" + Key + " " + Total + " " + Seconds + " " + Gain + " " + Average + " "
            + Usual + " " + Percent + " " + UsualPercent + " " + Length + " " + Found + ")";
    }
}
