#nullable enable
namespace TenGo.Core
{
    /// Graine de dix en cours (SeedGarden.Seed). Dates en millisecondes Unix.
    /// Structure : l'égalité compare tous les champs.
    public readonly struct Seed
    {
        public Seed(long plantedAtMs, int chain, long bloomAtMs)
        {
            PlantedAtMs = plantedAtMs;
            Chain = chain;
            BloomAtMs = bloomAtMs;
        }

        public long PlantedAtMs { get; }
        /// Plus longue chaîne qui a planté (ou renforcé) la graine.
        public int Chain { get; }
        public long BloomAtMs { get; }

        /// Multiplicateur de la future fleur.
        public int Power => SeedRules.Power(Chain);

        public Seed WithChain(int chain) => new Seed(PlantedAtMs, chain, BloomAtMs);
    }
}
