#nullable enable
namespace TenGo.Core
{
    /// Graines de dix, règles pures (SeedGarden.swift) : la plus longue chaîne
    /// d'une partie (≥ 4) plante une graine qui éclot en 4 h en « fleur de dix »,
    /// bulle qui multiplie les points de la chaîne qui la contient dans la
    /// partie suivante. Un seul emplacement.
    public static class SeedRules
    {
        /// Longueur de chaîne minimale pour planter.
        public const int MinimumChain = 4;
        /// Temps de pousse (4 h).
        public const long GrowthMs = 4L * 3_600_000L;

        /// Multiplicateur de la fleur selon la chaîne qui a planté la graine.
        public static int Power(int chain)
        {
            if (chain < MinimumChain) return 1;
            if (chain == 4) return 2;
            if (chain <= 6) return 3;
            return 4;
        }

        public static bool IsBloomed(Seed seed, long nowMs) => nowMs >= seed.BloomAtMs;

        /// Propose la plus longue chaîne de la partie qui vient de finir : renvoie
        /// la graine à garder (null : aucune) et ce que la fin de partie en dit.
        /// `growthMs` n'est raccourci que par l'app de test.
        public static (Seed? seed, SeedOutcome outcome) Offer(Seed? current, int chain, long nowMs, long growthMs = GrowthMs)
        {
            if (current is Seed existing)
            {
                if (IsBloomed(existing, nowMs)) return (existing, SeedOutcome.FlowerWaiting(existing.Power));
                if (Power(chain) > existing.Power)
                {
                    Seed stronger = existing.WithChain(chain);
                    return (stronger, SeedOutcome.Strengthened(stronger.Power, stronger.BloomAtMs));
                }
                return (existing, SeedOutcome.StillGrowing(existing.BloomAtMs));
            }
            if (chain < MinimumChain) return (null, SeedOutcome.TooShort(MinimumChain));
            var planted = new Seed(nowMs, chain, nowMs + growthMs);
            return (planted, SeedOutcome.Planted(planted.Power, planted.BloomAtMs));
        }
    }
}
