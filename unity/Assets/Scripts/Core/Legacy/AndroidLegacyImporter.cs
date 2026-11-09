#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TenGo.Core
{
    /// Conversion des données de l'app Android 3.3.0 (Kotlin) vers les clés et
    /// formats iOS de `SaveKeys`. Chaque clé Kotlin connue est soit convertie,
    /// soit explicitement ignorée avec sa raison (`Rules`) ; une clé inconnue
    /// est ignorée et signalée.
    ///
    /// Divergences de format traitées (cf. MIGRATION.md, étape 2) :
    ///  - série : numéro de jour epoch en chaîne → début du jour local en secondes Swift ;
    ///  - miroir : une clé `tenGO_mirrorData` en ms → `tenGO_mirrorRecords` + `tenGO_mirrorPartitions` ;
    ///  - graine : `{plantedAtMs, chain, bloomAtMs}` → `{plantedAt, chain, bloomAt}` ;
    ///  - meilleurs scores et historique de duels : CSV → tableaux ;
    ///  - ensembles (thèmes, styles, succès) → tableaux triés ;
    ///  - quota des vidéos récompensées : autres noms de clés ;
    ///  - premier lancement et pubs : ms epoch → secondes Swift, jour AAAAMMJJ entier → chaîne.
    public static class AndroidLegacyImporter
    {
        /// Fichier source d'une clé Kotlin.
        public enum SourceFile
        {
            DataStore,
            FirstRun,
            Ads,
        }

        private delegate void Converter(string key, LegacyValue value, LegacyConversion output, TimeZoneInfo zone);

        private sealed class Rule
        {
            public Rule(string description, Converter convert)
            {
                Description = description;
                Convert = convert;
            }

            public string Description { get; }
            public Converter Convert { get; }
        }

        // MARK: - Règles

        private static Rule Same() => new Rule("identique", (k, v, o, z) => o.Values[k] = v);

        private static Rule Rename(string target) => new Rule("→ " + target, (k, v, o, z) => o.Values[target] = v);

        private static Rule Ignore(string reason) => new Rule("ignorée : " + reason, (k, v, o, z) => o.Ignore(k, reason));

        private static readonly Rule CsvInts = new Rule("CSV → tableau d'entiers", (k, v, o, z) =>
        {
            if (v.Kind != LegacyValueKind.String) { o.Ignore(k, "type " + v.Kind + " inattendu"); return; }
            var ints = new List<long>();
            foreach (string part in (v.String ?? "").Split(','))
                if (long.TryParse(part.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out long n)) ints.Add(n);
            o.Values[k] = LegacyValue.OfIntList(ints);
        });

        private static readonly Rule CsvStrings = new Rule("CSV → tableau de chaînes", (k, v, o, z) =>
        {
            if (v.Kind != LegacyValueKind.String) { o.Ignore(k, "type " + v.Kind + " inattendu"); return; }
            o.Values[k] = LegacyValue.OfStringList((v.String ?? "").Split(',').Where(s => !string.IsNullOrWhiteSpace(s)));
        });

        private static readonly Rule SetToList = new Rule("ensemble → tableau trié", (k, v, o, z) =>
        {
            if (!v.IsStrings) { o.Ignore(k, "type " + v.Kind + " inattendu"); return; }
            o.Values[k] = LegacyValue.OfStringList(v.Strings.OrderBy(s => s, StringComparer.Ordinal));
        });

        /// `toEpochDay()` en chaîne → début du jour local (`startOfDay`) en secondes Swift.
        private static readonly Rule EpochDayToStartOfDay = new Rule("jour epoch → début du jour local (secondes Swift)", (k, v, o, z) =>
        {
            long day;
            if (v.Kind == LegacyValueKind.String && long.TryParse(v.String, NumberStyles.Integer, CultureInfo.InvariantCulture, out long parsed)) day = parsed;
            else if (v.IsInteger) day = v.Long;
            else { o.Ignore(k, "jour epoch illisible"); return; }
            if (day < 0 || day > 2_932_896) { o.Ignore(k, "jour epoch hors limites"); return; }
            o.Values[k] = LegacyValue.OfDouble(SwiftDate.ToReferenceSeconds(StartOfEpochDay(day, z)));
        });

        /// Instant en ms epoch → secondes Swift ; une valeur ≤ 0 (« jamais ») est omise.
        private static readonly Rule MsToReferenceSeconds = new Rule("ms epoch → secondes Swift", (k, v, o, z) =>
        {
            if (!v.IsInteger) { o.Ignore(k, "type " + v.Kind + " inattendu"); return; }
            if (v.Long <= 0) { o.Ignore(k, "jamais (valeur par défaut)"); return; }
            o.Values[k] = LegacyValue.OfDouble(SwiftDate.UnixMsToReferenceSeconds(v.Long));
        });

        private static readonly Rule IntToString = new Rule("entier AAAAMMJJ → chaîne", (k, v, o, z) =>
        {
            if (!v.IsInteger) { o.Ignore(k, "type " + v.Kind + " inattendu"); return; }
            o.Values[k] = LegacyValue.OfString(v.Long.ToString(CultureInfo.InvariantCulture));
        });

        /// Partie en cours : JSON Kotlin (`id` par bulle, `mode`) → JSON iOS.
        private static readonly Rule SavedGameRule = new Rule("JSON Kotlin → JSON iOS (sans id ni mode)", (k, v, o, z) =>
        {
            if (v.Kind != LegacyValueKind.String) { o.Ignore(k, "type " + v.Kind + " inattendu"); return; }
            try
            {
                string? mode = (string?)JObject.Parse(v.String!)["mode"];
                if (mode != null && mode != "normal") { o.Ignore(k, "mode " + mode + " non repris"); return; }
            }
            catch (JsonException)
            {
                o.Ignore(k, "JSON illisible");
                return;
            }
            SavedGame? save = SavedGame.FromJson(v.String!);
            if (save == null) { o.Ignore(k, "partie illisible"); return; }
            o.Values[k] = LegacyValue.OfString(save.ToJson());
        });

        /// Miroir : `{"records":[{dateMs…}], "discovered":{clé: ms}}` → deux clés iOS.
        private static readonly Rule MirrorRule = new Rule("→ " + SaveKeys.MirrorRecords + " + " + SaveKeys.MirrorPartitions, (k, v, o, z) =>
        {
            if (v.Kind != LegacyValueKind.String) { o.Ignore(k, "type " + v.Kind + " inattendu"); return; }
            try
            {
                JObject root = JObject.Parse(v.String!);
                var records = (root["records"] as JArray ?? new JArray()).OfType<JObject>().Select(r => new MirrorRecord(
                    (long)r["dateMs"]!,
                    (int?)r["moves"] ?? 0,
                    (int?)r["lengthSum"] ?? 0,
                    (double?)r["lookSum"] ?? 0,
                    (int?)r["lookCount"] ?? 0,
                    (int?)r["fallChains"] ?? 0,
                    (int?)r["fallEligible"] ?? 0)).ToList();
                var discovered = (root["discovered"] as JObject ?? new JObject()).Properties()
                    .Select(p => new KeyValuePair<string, long>(p.Name, (long)p.Value));
                o.Values[SaveKeys.MirrorRecords] = LegacyValue.OfString(MirrorRepository.EncodeRecords(records));
                o.Values[SaveKeys.MirrorPartitions] = LegacyValue.OfString(MirrorRepository.EncodePartitions(discovered));
            }
            catch (Exception e) when (e is JsonException || e is FormatException || e is ArgumentException || e is InvalidCastException || e is NullReferenceException)
            {
                o.Ignore(k, "miroir illisible");
            }
        });

        /// Graine : `{plantedAtMs, chain, bloomAtMs}` → `{plantedAt, chain, bloomAt}` (secondes Swift).
        private static readonly Rule SeedRule = new Rule("ms → secondes Swift (plantedAt, bloomAt)", (k, v, o, z) =>
        {
            if (v.Kind != LegacyValueKind.String) { o.Ignore(k, "type " + v.Kind + " inattendu"); return; }
            try
            {
                JObject s = JObject.Parse(v.String!);
                var seed = new Seed((long)s["plantedAtMs"]!, (int)s["chain"]!, (long)s["bloomAtMs"]!);
                o.Values[k] = LegacyValue.OfString(SeedRepository.Encode(seed));
            }
            catch (Exception e) when (e is JsonException || e is FormatException || e is ArgumentException || e is InvalidCastException || e is NullReferenceException)
            {
                o.Ignore(k, "graine illisible");
            }
        });

        /// Clés de `Prefs.kt` (et de `MirrorRepository.kt`) → règle.
        private static readonly Dictionary<string, Rule> DataStoreRules = new Dictionary<string, Rule>
        {
            ["tengo_saved_game"] = SavedGameRule,
            ["tengo_high_scores"] = CsvInts,
            ["tengo_rush_high_scores"] = CsvInts,
            ["tenGO_soundMuted"] = Same(),
            ["tenGO_hapticsEnabled"] = Same(),
            ["tenGO_reducedEffects"] = Same(),
            ["hasSeenTutorial"] = Same(),
            ["hasSeenShopPurchaseGuide"] = Same(),
            ["tenGO_shopGuideSnooze"] = Same(),
            ["tenGO_coins"] = Same(),
            ["tenGO_dailyLastCompleted"] = Same(),
            ["tenGO_noAdsPurchased"] = Same(),
            // Kotlin note « bonus déjà versé » ; iOS note « bonus en attente »
            // (achat différé). Un bonus Android est versé dans la foulée de
            // l'achat : il n'y a jamais rien en attente à reprendre.
            ["tenGO_noAdsBonusGranted"] = Ignore("bonus déjà versé ; iOS ne garde qu'un drapeau « en attente »"),
            ["tenGO_streakCurrent"] = Same(),
            ["tenGO_streakBest"] = Same(),
            ["tenGO_streakLastPlayed"] = EpochDayToStartOfDay,
            ["tenGO_streakRewardedMilestone"] = Same(),
            ["tenGO_streakShieldCount"] = Same(),
            ["tenGO_streakMilestoneMigrated"] = Same(),
            ["tenGO_totalXP"] = Same(),
            ["tenGO_missionsDayKey"] = Same(),
            ["tenGO_missionsProgress"] = Same(), // même JSON [{"id","value","claimed"}]
            ["tenGO_statsBestChain"] = Same(),
            ["tenGO_statsPerfectTotal"] = Same(),
            ["tenGO_statsTotalChains"] = Same(),
            ["tenGO_statsTotalRushGames"] = Same(),
            ["tenGO_statsTotalDaily"] = Same(),
            ["tenGO_achievementsUnlocked"] = SetToList,
            ["tenGO_duelHistory"] = CsvStrings,
            ["tenGO_notifGamesPlayed"] = Same(),
            ["tenGO_notifRequested"] = Same(),
            ["tenGO_rewardedDayKey"] = Rename(SaveKeys.RewardedDayKey),
            ["tenGO_rewardedCount"] = Rename(SaveKeys.RewardedDayCount),
            ["tenGO_ownedThemes"] = SetToList,
            ["tenGO_activeTheme"] = Same(),
            ["tenGO_ownedBubbleStyles"] = SetToList,
            ["tenGO_activeBubbleStyle"] = Same(),
            ["tenGO_ownedTrails"] = SetToList,
            ["tenGO_activeTrail"] = Same(),
            ["tenGO_mirrorData"] = MirrorRule,
            ["tenGO_seedGarden"] = SeedRule,
            ["tenGO_seedFlowerExplained"] = Same(),
        };

        /// Préfixes de `Prefs.kt` (clés composées, mêmes noms qu'iOS).
        private static readonly (string prefix, Rule rule)[] DataStorePrefixRules =
        {
            (SaveKeys.BoosterInventoryPrefix, Same()),
            (SaveKeys.BoosterCoachSeenPrefix, Same()),
            (SaveKeys.PuzzleStarsPrefix, Same()),
        };

        /// `FirstRun.kt` (SharedPreferences `tengo_first_run`).
        private static readonly Dictionary<string, Rule> FirstRunRules = new Dictionary<string, Rule>
        {
            [SaveKeys.FirstRunState] = Same(),
            [SaveKeys.FirstOpenAt] = MsToReferenceSeconds,
            [SaveKeys.FirstGameStartLogged] = Same(),
        };

        /// `AdsManager.kt` (SharedPreferences `tengo_ads`).
        private static readonly Dictionary<string, Rule> AdsRules = new Dictionary<string, Rule>
        {
            [SaveKeys.InterstitialLastShownAt] = MsToReferenceSeconds,
            [SaveKeys.InterstitialsSinceNoAdsOffer] = Same(),
            [SaveKeys.InterstitialsTodayCount] = Same(),
            [SaveKeys.InterstitialsTodayDay] = IntToString,
        };

        /// Table lisible de la correspondance (documentation, tests).
        public static IEnumerable<(SourceFile file, string key, string conversion)> Rules =>
            DataStoreRules.Select(p => (SourceFile.DataStore, p.Key, p.Value.Description))
                .Concat(DataStorePrefixRules.Select(p => (SourceFile.DataStore, p.prefix + "*", p.rule.Description)))
                .Concat(FirstRunRules.Select(p => (SourceFile.FirstRun, p.Key, p.Value.Description)))
                .Concat(AdsRules.Select(p => (SourceFile.Ads, p.Key, p.Value.Description)));

        // MARK: - Conversion

        /// Convertit les trois sources. `zone` : fuseau du joueur, pour placer
        /// le jour de série en début de journée locale comme iOS.
        public static LegacyConversion Convert(AndroidLegacyData data, TimeZoneInfo zone)
        {
            var output = new LegacyConversion("android");
            Apply(data.Preferences, DataStoreRules, DataStorePrefixRules, "", output, zone);
            Apply(data.FirstRun, FirstRunRules, Array.Empty<(string, Rule)>(), AndroidLegacyData.FirstRunFile + ":", output, zone);
            Apply(data.Ads, AdsRules, Array.Empty<(string, Rule)>(), AndroidLegacyData.AdsFile + ":", output, zone);
            return output;
        }

        private static void Apply(
            IReadOnlyDictionary<string, LegacyValue> source,
            Dictionary<string, Rule> rules,
            (string prefix, Rule rule)[] prefixRules,
            string ignoredLabel,
            LegacyConversion output,
            TimeZoneInfo zone)
        {
            foreach (var pair in source.OrderBy(p => p.Key, StringComparer.Ordinal))
            {
                Rule? rule = rules.TryGetValue(pair.Key, out Rule r) ? r : null;
                if (rule == null)
                {
                    foreach (var (prefix, prefixRule) in prefixRules)
                        if (pair.Key.StartsWith(prefix, StringComparison.Ordinal) && LegacySchema.TypeOf(pair.Key) != null) rule = prefixRule;
                }
                if (rule == null)
                {
                    output.Ignore(ignoredLabel + pair.Key, "clé inconnue");
                    continue;
                }
                var sink = new LegacyConversion(output.Source);
                rule.Convert(pair.Key, pair.Value, sink, zone);
                foreach (var v in sink.Values) output.Values[v.Key] = v.Value;
                foreach (var i in sink.Ignored) output.Ignore(ignoredLabel + i.Key, i.Value);
            }
        }

        /// Minuit local du jour epoch `day` (`LocalDate.ofEpochDay`), comme
        /// `Calendar.startOfDay(for:)` : si minuit n'existe pas (passage à
        /// l'heure d'été à 0 h), on prend le premier instant valide.
        public static DateTimeOffset StartOfEpochDay(long day, TimeZoneInfo zone)
        {
            DateTime midnight = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Unspecified).AddDays(day);
            TimeSpan offset = zone.IsInvalidTime(midnight) ? zone.GetUtcOffset(midnight.AddHours(1)) : zone.GetUtcOffset(midnight);
            return new DateTimeOffset(midnight, offset);
        }
    }
}
