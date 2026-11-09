#nullable enable
namespace TenGo.Core
{
    /// Sort de la graine en fin de partie. Champs non concernés à 0.
    /// Structure : l'égalité compare tous les champs.
    public readonly struct SeedOutcome
    {
        private SeedOutcome(SeedOutcomeKind kind, int power, long bloomAtMs, int minimum)
        {
            Kind = kind;
            Power = power;
            BloomAtMs = bloomAtMs;
            Minimum = minimum;
        }

        public SeedOutcomeKind Kind { get; }
        /// Planted, Strengthened, FlowerWaiting.
        public int Power { get; }
        /// Planted, Strengthened, StillGrowing.
        public long BloomAtMs { get; }
        /// TooShort.
        public int Minimum { get; }

        public static SeedOutcome Planted(int power, long bloomAtMs) => new SeedOutcome(SeedOutcomeKind.Planted, power, bloomAtMs, 0);
        public static SeedOutcome Strengthened(int power, long bloomAtMs) => new SeedOutcome(SeedOutcomeKind.Strengthened, power, bloomAtMs, 0);
        public static SeedOutcome StillGrowing(long bloomAtMs) => new SeedOutcome(SeedOutcomeKind.StillGrowing, 0, bloomAtMs, 0);
        public static SeedOutcome FlowerWaiting(int power) => new SeedOutcome(SeedOutcomeKind.FlowerWaiting, power, 0, 0);
        public static SeedOutcome TooShort(int minimum) => new SeedOutcome(SeedOutcomeKind.TooShort, 0, 0, minimum);

        public override string ToString() => Kind + "(power " + Power + ", bloomAt " + BloomAtMs + ", min " + Minimum + ")";
    }
}
