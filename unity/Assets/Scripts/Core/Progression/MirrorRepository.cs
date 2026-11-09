#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TenGo.Core
{
    /// Persistance du miroir cognitif (CognitiveMirror.swift), aux clés et au
    /// format d'iOS : `tenGO_mirrorRecords` (tableau de parties) et
    /// `tenGO_mirrorPartitions` (décomposition → date), dates en secondes de
    /// référence Swift.
    public sealed class MirrorRepository
    {
        private readonly ISaveStore _store;
        private readonly IClock _clock;

        public MirrorRepository(ISaveStore store, IClock clock)
        {
            _store = store;
            _clock = clock;
        }

        private long NowMs => _clock.Now.ToUnixTimeMilliseconds();

        public MirrorData Data => new MirrorData(LoadRecords(), LoadDiscovered());

        public int GamesMeasured => LoadRecords().Count;

        public int RepertoireCount => LoadDiscovered().Count;

        public MirrorReading WeeklyReading(MirrorSkill skill) => Mirror.WeeklyReading(Data, skill, NowMs);

        /// Archive la partie et renvoie la phrase de fin (une fois par partie
        /// terminée, hors démo).
        public MirrorInsight? FinishGame(MirrorTally tally)
        {
            MirrorData before = Data;
            var (updated, insight) = Mirror.FinishGame(before, tally, NowMs);
            if (!ReferenceEquals(updated, before)) Save(updated);
            return insight;
        }

        public void Save(MirrorData data)
        {
            var records = new JArray(data.Records.Select(r => new JObject
            {
                ["date"] = SwiftDate.UnixMsToReferenceSeconds(r.DateMs),
                ["moves"] = r.Moves,
                ["lengthSum"] = r.LengthSum,
                ["lookSum"] = r.LookSum,
                ["lookCount"] = r.LookCount,
                ["fallChains"] = r.FallChains,
                ["fallEligible"] = r.FallEligible,
            }));
            var discovered = new JObject();
            foreach (var pair in data.Discovered) discovered[pair.Key] = SwiftDate.UnixMsToReferenceSeconds(pair.Value);
            _store.SetString(SaveKeys.MirrorRecords, records.ToString(Formatting.None));
            _store.SetString(SaveKeys.MirrorPartitions, discovered.ToString(Formatting.None));
        }

        /// QA : historique crédible pour l'écran « Ton cerveau » (seedForQA iOS).
        public void SeedForQa()
        {
            long now = NowMs;
            var records = Enumerable.Range(0, 12).Select(i => new MirrorRecord(
                now - (14 - i) * 86_400_000L, 18, (int)(18 * (2.6 + i * 0.06)), 18 * (3.2 - i * 0.12), 18, 4 + i / 2, 17)).ToList();
            var discovered = Partition.AllKeys.Take(17).Select((k, i) => (k, i))
                .ToDictionary(p => p.k, p => now - (20 - p.i) * 86_400_000L);
            Save(new MirrorData(records, discovered));
        }

        private List<MirrorRecord> LoadRecords()
        {
            string? json = _store.GetString(SaveKeys.MirrorRecords);
            if (json == null) return new List<MirrorRecord>();
            try
            {
                return JArray.Parse(json).OfType<JObject>().Select(o => new MirrorRecord(
                    SwiftDate.ReferenceSecondsToUnixMs((double)o["date"]!),
                    (int?)o["moves"] ?? 0,
                    (int?)o["lengthSum"] ?? 0,
                    (double?)o["lookSum"] ?? 0,
                    (int?)o["lookCount"] ?? 0,
                    (int?)o["fallChains"] ?? 0,
                    (int?)o["fallEligible"] ?? 0)).ToList();
            }
            catch (Exception e) when (e is JsonException || e is FormatException || e is ArgumentException)
            {
                return new List<MirrorRecord>();
            }
        }

        private Dictionary<string, long> LoadDiscovered()
        {
            string? json = _store.GetString(SaveKeys.MirrorPartitions);
            var result = new Dictionary<string, long>();
            if (json == null) return result;
            try
            {
                foreach (var prop in JObject.Parse(json).Properties())
                    result[prop.Name] = SwiftDate.ReferenceSecondsToUnixMs((double)prop.Value);
            }
            catch (Exception e) when (e is JsonException || e is FormatException || e is ArgumentException)
            {
                result.Clear();
            }
            return result;
        }
    }
}
