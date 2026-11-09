#nullable enable
namespace TenGo.Core
{
    /// Partie archivée par le miroir (MirrorGameRecord). Date en millisecondes
    /// Unix ; la persistance la convertit en date Swift.
    public sealed class MirrorRecord
    {
        public MirrorRecord(long dateMs, int moves, int lengthSum, double lookSum, int lookCount, int fallChains, int fallEligible)
        {
            DateMs = dateMs;
            Moves = moves;
            LengthSum = lengthSum;
            LookSum = lookSum;
            LookCount = lookCount;
            FallChains = fallChains;
            FallEligible = fallEligible;
        }

        public long DateMs { get; }
        public int Moves { get; }
        public int LengthSum { get; }
        public double LookSum { get; }
        public int LookCount { get; }
        public int FallChains { get; }
        public int FallEligible { get; }
    }
}
