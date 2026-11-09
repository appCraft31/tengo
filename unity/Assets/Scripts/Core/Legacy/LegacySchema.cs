#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TenGo.Core
{
    /// Schéma cible de la reprise : les clés iOS (`SaveKeys`) et leur type.
    /// C'est le format que lisent les dépôts de Core ; les importeurs iOS et
    /// Android produisent des valeurs dans ce schéma, puis `LegacyMigration`
    /// les écrit dans l'`ISaveStore` après normalisation (`Normalize`).
    public static class LegacySchema
    {
        /// Clés exactes, avec leur type.
        public static readonly IReadOnlyDictionary<string, SaveValueType> Keys = new Dictionary<string, SaveValueType>
        {
            [SaveKeys.HasSeenTutorial] = SaveValueType.Bool,
            [SaveKeys.HasSeenShopPurchaseGuide] = SaveValueType.Bool,
            [SaveKeys.ShopGuideSnooze] = SaveValueType.Int,
            [SaveKeys.SoundMuted] = SaveValueType.Bool,
            [SaveKeys.HapticsEnabled] = SaveValueType.Bool,
            [SaveKeys.ReducedEffects] = SaveValueType.Bool,
            [SaveKeys.DailyLastCompletedDayKey] = SaveValueType.Int,
            [SaveKeys.StreakCurrent] = SaveValueType.Int,
            [SaveKeys.StreakBest] = SaveValueType.Int,
            [SaveKeys.StreakLastPlayed] = SaveValueType.Double,
            [SaveKeys.NotifGamesPlayed] = SaveValueType.Int,
            [SaveKeys.NotifRequested] = SaveValueType.Bool,
            [SaveKeys.CoinsBalance] = SaveValueType.Int,
            [SaveKeys.StreakRewardedMilestone] = SaveValueType.Int,
            [SaveKeys.StreakShieldCount] = SaveValueType.Int,
            [SaveKeys.StreakMilestoneMigrated] = SaveValueType.Bool,
            [SaveKeys.OwnedThemes] = SaveValueType.StringList,
            [SaveKeys.ActiveTheme] = SaveValueType.String,
            [SaveKeys.OwnedBubbleStyles] = SaveValueType.StringList,
            [SaveKeys.ActiveBubbleStyle] = SaveValueType.String,
            [SaveKeys.OwnedTrails] = SaveValueType.StringList,
            [SaveKeys.ActiveTrail] = SaveValueType.String,
            [SaveKeys.RewardedDayKey] = SaveValueType.Int,
            [SaveKeys.RewardedDayCount] = SaveValueType.Int,
            [SaveKeys.NoAdsPurchased] = SaveValueType.Bool,
            [SaveKeys.NoAdsBonusPending] = SaveValueType.Bool,
            [SaveKeys.TotalXP] = SaveValueType.Int,
            [SaveKeys.MissionsDayKey] = SaveValueType.Int,
            [SaveKeys.MissionsProgress] = SaveValueType.Json,
            [SaveKeys.StatsBestChain] = SaveValueType.Int,
            [SaveKeys.StatsPerfectTotal] = SaveValueType.Int,
            [SaveKeys.StatsTotalChains] = SaveValueType.Int,
            [SaveKeys.StatsTotalRushGames] = SaveValueType.Int,
            [SaveKeys.StatsTotalDaily] = SaveValueType.Int,
            [SaveKeys.AchievementsUnlocked] = SaveValueType.StringList,
            [SaveKeys.DuelHistory] = SaveValueType.StringList,
            [SaveKeys.MirrorRecords] = SaveValueType.Json,
            [SaveKeys.MirrorPartitions] = SaveValueType.Json,
            [SaveKeys.SeedGarden] = SaveValueType.Json,
            [SaveKeys.SeedFlowerExplained] = SaveValueType.Bool,
            [SaveKeys.SavedGame] = SaveValueType.Json,
            [SaveKeys.HighScores] = SaveValueType.IntList,
            [SaveKeys.RushHighScores] = SaveValueType.IntList,
            [SaveKeys.InterstitialLastShownAt] = SaveValueType.Double,
            [SaveKeys.InterstitialsSinceNoAdsOffer] = SaveValueType.Int,
            [SaveKeys.InterstitialsTodayCount] = SaveValueType.Int,
            [SaveKeys.InterstitialsTodayDay] = SaveValueType.String,
            [SaveKeys.FirstRunState] = SaveValueType.String,
            [SaveKeys.FirstOpenAt] = SaveValueType.Double,
            [SaveKeys.FirstGameStartLogged] = SaveValueType.Bool,
        };

        /// Préfixes des clés composées, avec leur type.
        public static readonly IReadOnlyList<(string prefix, SaveValueType type)> Prefixes = new[]
        {
            (SaveKeys.BoosterInventoryPrefix, SaveValueType.Int),
            (SaveKeys.BoosterCoachSeenPrefix, SaveValueType.Bool),
            (SaveKeys.PuzzleStarsPrefix, SaveValueType.Int),
        };

        /// Type attendu d'une clé, ou null si elle n'appartient pas au jeu.
        public static SaveValueType? TypeOf(string key)
        {
            if (Keys.TryGetValue(key, out SaveValueType t)) return t;
            foreach (var (prefix, type) in Prefixes)
            {
                if (key.Length > prefix.Length && key.StartsWith(prefix, StringComparison.Ordinal) && IsValidSuffix(prefix, key.Substring(prefix.Length)))
                    return type;
            }
            return null;
        }

        /// Suffixe d'une clé composée : identifiant de booster (`[a-z]+`) ou
        /// `<monde>_<index>` pour les étoiles de puzzle.
        private static bool IsValidSuffix(string prefix, string suffix)
        {
            if (prefix == SaveKeys.PuzzleStarsPrefix)
            {
                string[] parts = suffix.Split('_');
                return parts.Length == 2 && parts.All(p => p.Length > 0 && p.All(char.IsDigit));
            }
            return suffix.All(c => c >= 'a' && c <= 'z');
        }

        /// Ramène une valeur au type attendu (tolérances d'UserDefaults :
        /// booléen stocké en entier, nombre en chaîne, `Data` JSON…). Renvoie
        /// null si la valeur est inutilisable, avec la raison.
        public static LegacyValue? Normalize(SaveValueType type, LegacyValue value, out string? reason)
        {
            reason = null;
            var inv = CultureInfo.InvariantCulture;
            switch (type)
            {
                case SaveValueType.Bool:
                    if (value.Kind == LegacyValueKind.Bool) return value;
                    if (value.IsInteger) return LegacyValue.OfBool(value.Long != 0);
                    if (value.Kind == LegacyValueKind.String && (value.String == "true" || value.String == "false"))
                        return LegacyValue.OfBool(value.String == "true");
                    break;
                case SaveValueType.Int:
                    if (value.IsInteger)
                    {
                        if (value.Long < int.MinValue || value.Long > int.MaxValue) { reason = "entier hors limites"; return null; }
                        return LegacyValue.OfInt(value.Long);
                    }
                    if (value.Kind == LegacyValueKind.Bool) return LegacyValue.OfInt(value.Bool ? 1 : 0);
                    if ((value.Kind == LegacyValueKind.Double || value.Kind == LegacyValueKind.Float) && Math.Abs(value.Double) <= int.MaxValue)
                        return LegacyValue.OfInt((long)value.Double); // `integer(forKey:)` tronque
                    if (value.Kind == LegacyValueKind.String && int.TryParse(value.String, NumberStyles.Integer, inv, out int parsed))
                        return LegacyValue.OfInt(parsed);
                    break;
                case SaveValueType.Double:
                    if (value.IsNumber)
                    {
                        double d = value.IsInteger ? value.Long : value.Double;
                        if (double.IsNaN(d) || double.IsInfinity(d)) { reason = "nombre non fini"; return null; }
                        return LegacyValue.OfDouble(d);
                    }
                    if (value.Kind == LegacyValueKind.String && double.TryParse(value.String, NumberStyles.Float, inv, out double pd))
                        return LegacyValue.OfDouble(pd);
                    break;
                case SaveValueType.String:
                    if (value.Kind == LegacyValueKind.String) return value;
                    if (value.IsInteger) return LegacyValue.OfString(value.Long.ToString(inv));
                    break;
                case SaveValueType.StringList:
                    if (value.IsStrings) return LegacyValue.OfStringList(value.Strings);
                    if (value.Kind == LegacyValueKind.IntList && value.Longs.Count == 0) return LegacyValue.OfStringList(Array.Empty<string>());
                    break;
                case SaveValueType.IntList:
                    if (value.Kind == LegacyValueKind.IntList)
                    {
                        if (value.Longs.Any(v => v < int.MinValue || v > int.MaxValue)) { reason = "entier hors limites"; return null; }
                        return value;
                    }
                    if (value.IsStrings && value.Strings.Count == 0) return LegacyValue.OfIntList(Array.Empty<long>());
                    break;
                case SaveValueType.Json:
                {
                    string? text = value.Kind == LegacyValueKind.String ? value.String
                        : value.Kind == LegacyValueKind.Bytes ? TryUtf8(value.Bytes) : null;
                    if (text == null) break;
                    try
                    {
                        JToken.Parse(text);
                    }
                    catch (JsonException)
                    {
                        reason = "JSON illisible";
                        return null;
                    }
                    return LegacyValue.OfString(text);
                }
            }
            reason = "type " + value.Kind + " inattendu pour " + type;
            return null;
        }

        private static string? TryUtf8(byte[] bytes)
        {
            try
            {
                return new UTF8Encoding(false, true).GetString(bytes);
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        /// Écrit une valeur déjà normalisée.
        public static void Write(ISaveStore store, string key, SaveValueType type, LegacyValue value)
        {
            switch (type)
            {
                case SaveValueType.Bool: store.SetBool(key, value.Bool); break;
                case SaveValueType.Int: store.SetInt(key, (int)value.Long); break;
                case SaveValueType.Double: store.SetDouble(key, value.Double); break;
                case SaveValueType.String:
                case SaveValueType.Json: store.SetString(key, value.String ?? ""); break;
                case SaveValueType.StringList: store.SetStringList(key, value.Strings); break;
                case SaveValueType.IntList: store.SetIntList(key, value.Longs.Select(v => (int)v)); break;
            }
        }
    }
}
