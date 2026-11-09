#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TenGo.Core
{
    /// Missions quotidiennes (MissionManager.swift) : 3 missions + 1 super
    /// mission, tirées chaque jour (bascule à 5 h comme le défi du jour) par une
    /// sélection déterministe identique sur toutes les plateformes. Les missions
    /// non réclamées de la veille sont perdues. Le renouvellement exige une clé
    /// STRICTEMENT supérieure : reculer l'horloge ne re-tire pas les missions
    /// (sinon la super mission serait farmable). Comme sur iOS, chaque accès
    /// renouvelle d'abord les missions si le jour a changé.
    public sealed class MissionRepository
    {
        /// Sel de la graine du jour : « MIS ».
        private const ulong SeedSalt = 0x4D4953UL;

        private readonly ISaveStore _store;
        private readonly EconomyRepository _economy;
        private readonly LevelRepository _level;
        private readonly StreakRepository _streak;
        private readonly IClock _clock;
        private readonly IRng _claimRng;

        /// `claimRng` tire le bouclier de la super mission (générateur système en jeu).
        public MissionRepository(ISaveStore store, EconomyRepository economy, LevelRepository level,
            StreakRepository streak, IClock clock, IRng claimRng)
        {
            _store = store;
            _economy = economy;
            _level = level;
            _streak = streak;
            _clock = clock;
            _claimRng = claimRng;
        }

        // MARK: - Lecture

        /// Missions du jour, dans un ordre stable (renouvelées si besoin).
        public IReadOnlyList<DisplayMission> TodaysMissions
        {
            get
            {
                RolloverIfNeeded();
                var result = new List<DisplayMission>();
                foreach (MissionEntry e in LoadEntries())
                {
                    MissionDefinition? def = MissionCatalog.Definition(e.Id);
                    if (def != null) result.Add(new DisplayMission(def, e.Value, e.Claimed));
                }
                return result;
            }
        }

        /// Au moins une mission complétée mais pas réclamée (pastille du menu).
        public bool HasUnclaimedReward => TodaysMissions.Any(m => m.IsCompleted && !m.Claimed);

        // MARK: - Renouvellement

        private void RolloverIfNeeded() => EnsureRolled(DayKeys.Challenge(_clock));

        /// Tire les missions de `dayKey` si la clé stockée est plus ancienne.
        public void EnsureRolled(int dayKey)
        {
            int stored = _store.GetInt(SaveKeys.MissionsDayKey);
            if (dayKey <= stored) return;
            SaveEntries(RollFor(dayKey));
            _store.SetInt(SaveKeys.MissionsDayKey, dayKey);
        }

        /// Sélection déterministe : graine `UInt64(bitPattern: Int64(dayKey)) &+ 0x4D4953`,
        /// mélange Swift des missions régulières puis tirage de la super mission.
        public static List<MissionEntry> RollFor(int dayKey)
        {
            var rng = new SeededGenerator(unchecked((ulong)(long)dayKey + SeedSalt));
            var chosen = MissionCatalog.Regular.ShuffledSwift(rng).Take(MissionCatalog.MissionsPerDay).ToList();
            if (MissionCatalog.SuperMissions.TryRandomElementSwift(rng, out MissionDefinition superOne)) chosen.Add(superOne);
            return chosen.Select(d => new MissionEntry(d.Id, 0, false)).ToList();
        }

        // MARK: - Progression en jeu

        /// Un chemin de `length` bulles vient d'être validé.
        public void ReportChain(int length) =>
            Update((def, value) => def.Kind == MissionKind.ChainAtLeast && length >= def.Param && value < def.Target ? value + 1 : value);

        /// Un chemin (quel qu'il soit) vient d'être validé.
        public void ReportMove() => Update((def, value) => def.Kind == MissionKind.MovesPlayed ? value + 1 : value);

        /// `delta` points viennent d'être marqués (en direct pendant la partie).
        public void ReportScore(int delta)
        {
            if (delta <= 0) return;
            Update((def, value) => def.Kind == MissionKind.CumulativeScore ? value + delta : value);
        }

        /// Une grille vient d'être vidée (en Rush, plusieurs par partie).
        public void ReportPerfectBoard() => Update((def, value) => def.Kind == MissionKind.PerfectBoards ? value + 1 : value);

        /// Fin de partie : compte pour `GamesPlayed` et, grille vidée, pour `PerfectBoards`.
        public void ReportGameEnded(bool isPerfect) => Update((def, value) => def.Kind switch
        {
            MissionKind.GamesPlayed => value + 1,
            MissionKind.PerfectBoards => isPerfect ? value + 1 : value,
            _ => value,
        });

        private void Update(Func<MissionDefinition, int, int> transform)
        {
            RolloverIfNeeded();
            List<MissionEntry> entries = LoadEntries();
            bool changed = false;
            foreach (MissionEntry e in entries)
            {
                MissionDefinition? def = MissionCatalog.Definition(e.Id);
                if (def == null || e.Claimed) continue;
                int next = Math.Min(def.Target, transform(def, e.Value));
                if (next != e.Value)
                {
                    e.Value = next;
                    changed = true;
                }
            }
            if (changed) SaveEntries(entries);
        }

        // MARK: - Réclamation

        /// Réclame une mission complétée : pièces, XP, et 1 chance sur 4 d'un
        /// bouclier pour la super mission. `null` si déjà réclamée ou incomplète.
        public MissionClaimResult? Claim(string id)
        {
            RolloverIfNeeded();
            List<MissionEntry> entries = LoadEntries();
            MissionEntry? entry = entries.FirstOrDefault(e => e.Id == id);
            MissionDefinition? def = MissionCatalog.Definition(id);
            if (entry == null || entry.Claimed || def == null || entry.Value < def.Target) return null;

            entry.Claimed = true;
            SaveEntries(entries);
            _economy.AddCoins(def.CoinReward);
            GainResult xp = _level.AwardForMission(def.IsSuper);
            bool shield = def.IsSuper && _claimRng.NextIntBelow(4) == 0;
            if (shield) _streak.AddShield(1);
            return new MissionClaimResult(def.CoinReward, xp, shield);
        }

        // MARK: - Persistance (JSON `[{"id","value","claimed"}]`)

        private List<MissionEntry> LoadEntries()
        {
            string? json = _store.GetString(SaveKeys.MissionsProgress);
            if (json == null) return new List<MissionEntry>();
            try
            {
                return JArray.Parse(json)
                    .OfType<JObject>()
                    .Select(o => new MissionEntry((string)o["id"]!, (int?)o["value"] ?? 0, (bool?)o["claimed"] ?? false))
                    .ToList();
            }
            catch (Exception e) when (e is JsonException || e is FormatException || e is ArgumentException)
            {
                return new List<MissionEntry>();
            }
        }

        private void SaveEntries(IEnumerable<MissionEntry> entries)
        {
            var array = new JArray(entries.Select(e => new JObject { ["id"] = e.Id, ["value"] = e.Value, ["claimed"] = e.Claimed }));
            _store.SetString(SaveKeys.MissionsProgress, array.ToString(Formatting.None));
        }
    }
}
