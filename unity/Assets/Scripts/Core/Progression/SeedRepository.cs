#nullable enable
using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TenGo.Core
{
    /// Persistance de la graine de dix (SeedGarden.swift), à la clé et au
    /// format d'iOS : `{"plantedAt","chain","bloomAt"}` en secondes de
    /// référence Swift. Un seul emplacement.
    public sealed class SeedRepository
    {
        private readonly ISaveStore _store;
        private readonly IClock _clock;

        public SeedRepository(ISaveStore store, IClock clock)
        {
            _store = store;
            _clock = clock;
        }

        /// Durée de pousse (raccourcie dans l'app de test).
        public long GrowthMs { get; set; } = SeedRules.GrowthMs;

        private long NowMs => _clock.Now.ToUnixTimeMilliseconds();

        /// Graine en cours (null : emplacement vide).
        public Seed? Current
        {
            get
            {
                string? json = _store.GetString(SaveKeys.SeedGarden);
                if (json == null) return null;
                try
                {
                    JObject o = JObject.Parse(json);
                    return new Seed(
                        SwiftDate.ReferenceSecondsToUnixMs((double)o["plantedAt"]!),
                        (int)o["chain"]!,
                        SwiftDate.ReferenceSecondsToUnixMs((double)o["bloomAt"]!));
                }
                catch (Exception e) when (e is JsonException || e is FormatException || e is ArgumentException || e is NullReferenceException)
                {
                    return null;
                }
            }
            private set
            {
                if (value is Seed seed)
                {
                    _store.SetString(SaveKeys.SeedGarden, Encode(seed));
                }
                else
                {
                    _store.Remove(SaveKeys.SeedGarden);
                }
            }
        }

        /// JSON iOS de `tenGO_seedGarden` : `{"plantedAt","chain","bloomAt"}`
        /// en secondes de référence Swift.
        public static string Encode(Seed seed) => new JObject
        {
            ["plantedAt"] = SwiftDate.UnixMsToReferenceSeconds(seed.PlantedAtMs),
            ["chain"] = seed.Chain,
            ["bloomAt"] = SwiftDate.UnixMsToReferenceSeconds(seed.BloomAtMs),
        }.ToString(Formatting.None);

        /// Une fleur attend-elle d'être posée ?
        public bool IsBloomed => Current is Seed s && SeedRules.IsBloomed(s, NowMs);

        /// Propose la plus longue chaîne de la partie qui vient de finir.
        public (Seed? seed, SeedOutcome outcome) Offer(int chain)
        {
            var result = SeedRules.Offer(Current, chain, NowMs, GrowthMs);
            Current = result.seed;
            return result;
        }

        /// Prend la fleur éclose pour une nouvelle partie ; renvoie son
        /// multiplicateur, ou null s'il n'y en a pas.
        public int? TakeFlower()
        {
            if (!(Current is Seed s) || !SeedRules.IsBloomed(s, NowMs)) return null;
            Current = null;
            return s.Power;
        }

        /// La toute première fleur posée est expliquée une seule fois.
        public bool FlowerExplained
        {
            get => _store.GetBool(SaveKeys.SeedFlowerExplained);
            set => _store.SetBool(SaveKeys.SeedFlowerExplained, value);
        }

        /// QA : graine éclose tout de suite.
        public void ForceBloomForQa(int chain = 6)
        {
            long now = NowMs;
            Current = new Seed(now - SeedRules.GrowthMs, chain, now - 1000);
        }

        public void ClearForQa() => Current = null;
    }
}
