#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json.Linq;

namespace TenGo.Core
{
    /// Textes localisés (`L("clé")` / `String(localized:)` d'iOS). La table vient
    /// de `Resources/Localization/strings.json`, généré depuis
    /// `Localizable.xcstrings` (`Tools/i18n/gen_strings.py`) ; Core ne lit pas les
    /// ressources : la couche jeu passe le texte JSON à `Load`.
    ///
    /// Langue absente : anglais, puis langue source (français). Clé absente : la
    /// clé elle-même, comme `String(localized:)` sans traduction.
    public static class Loc
    {
        private static Dictionary<string, Dictionary<string, string>> _strings = new Dictionary<string, Dictionary<string, string>>();
        private static List<string> _languages = new List<string>();
        private static string _source = "fr";

        /// Langue active (code iOS : `fr`, `en`, `pt-BR`, `zh-Hans`…).
        public static string Language { get; private set; } = "fr";

        public static IReadOnlyList<string> Languages => _languages;

        public static bool IsLoaded => _strings.Count > 0;

        /// Charge la table et choisit la langue la plus proche de `preferred`.
        public static void Load(string json, string? preferred)
        {
            JObject root = JObject.Parse(json);
            _source = (string?)root["source"] ?? "fr";
            _languages = new List<string>();
            if (root["languages"] is JArray langs)
                foreach (JToken l in langs) _languages.Add((string)l!);
            var table = new Dictionary<string, Dictionary<string, string>>();
            if (root["strings"] is JObject strings)
            {
                foreach (KeyValuePair<string, JToken?> entry in strings)
                {
                    var per = new Dictionary<string, string>();
                    if (entry.Value is JObject o)
                        foreach (KeyValuePair<string, JToken?> t in o) per[t.Key] = (string?)t.Value ?? "";
                    table[entry.Key] = per;
                }
            }
            _strings = table;
            Language = Resolve(preferred);
        }

        /// Change de langue (tests, captures).
        public static void Use(string? language) => Language = Resolve(language);

        /// Code de langue pris en charge le plus proche (`pt-PT` → `pt-BR`,
        /// `zh-Hant` → `zh-Hans`, inconnu → `en`).
        public static string Resolve(string? preferred)
        {
            if (_languages.Count == 0) return preferred ?? "fr";
            if (string.IsNullOrEmpty(preferred)) return _languages.Contains("en") ? "en" : _source;
            string p = preferred!.Replace('_', '-');
            foreach (string l in _languages)
                if (string.Equals(l, p, StringComparison.OrdinalIgnoreCase)) return l;
            string prefix = p.Split('-')[0];
            foreach (string l in _languages)
                if (string.Equals(l.Split('-')[0], prefix, StringComparison.OrdinalIgnoreCase)) return l;
            return _languages.Contains("en") ? "en" : _source;
        }

        /// Texte de `key` dans la langue active.
        public static string T(string key)
        {
            if (!_strings.TryGetValue(key, out Dictionary<string, string>? per)) return key;
            if (per.TryGetValue(Language, out string? s)) return s;
            if (per.TryGetValue("en", out s)) return s;
            if (per.TryGetValue(_source, out s)) return s;
            return key;
        }

        /// `String(format: L(key), args…)` ; les nombres sont écrits sans séparateur
        /// de milliers, comme `%d` / `%lld` de Swift.
        public static string F(string key, params object[] args)
        {
            string format = T(key);
            try
            {
                return string.Format(CultureInfo.InvariantCulture, format, args);
            }
            catch (FormatException)
            {
                return format;
            }
        }

        /// Existe-t-il une traduction pour `key` ?
        public static bool Has(string key) => _strings.ContainsKey(key);
    }
}
