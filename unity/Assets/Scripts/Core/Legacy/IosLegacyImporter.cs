#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TenGo.Core
{
    /// Reprise des UserDefaults de l'app iOS 3.3.0. Les clés et formats sont
    /// déjà ceux de Core : il suffit de filtrer les clés du jeu (le domaine
    /// `standardUserDefaults` contient aussi celles d'UMP, de Firebase, et les
    /// `PlayerPrefs` d'Unity) et de convertir les types Cocoa (`NSDate`, `NSData`).
    ///
    /// Le greffon `Plugins/iOS/TenGoLegacy.mm` lit les clés demandées par
    /// `RequestJson` et renvoie un JSON typé `{clé: {"t": type, "v": valeur}}`,
    /// types : `bool`, `int`, `double`, `string`, `date` (secondes Swift),
    /// `data` (NSData UTF-8, en texte), `bytes` (NSData binaire, base64),
    /// `array` (tableau de nombres ou de chaînes), `unsupported`.
    public static class IosLegacyImporter
    {
        /// Demande passée au greffon : clés exactes et préfixes du jeu.
        public static string RequestJson() => new JObject
        {
            ["keys"] = new JArray(LegacySchema.Keys.Keys.OrderBy(k => k, StringComparer.Ordinal)),
            ["prefixes"] = new JArray(LegacySchema.Prefixes.Select(p => p.prefix)),
        }.ToString(Formatting.None);

        /// Décode la réponse du greffon. Lève `FormatException` si le JSON est illisible.
        public static Dictionary<string, LegacyValue> ParsePluginJson(string json)
        {
            JObject root;
            try
            {
                root = JObject.Parse(json);
            }
            catch (JsonException e)
            {
                throw new FormatException("UserDefaults : JSON du greffon illisible", e);
            }
            var result = new Dictionary<string, LegacyValue>();
            foreach (JProperty prop in root.Properties())
            {
                if (!(prop.Value is JObject entry)) continue;
                LegacyValue? value = ParseEntry((string?)entry["t"], entry["v"]);
                if (value != null) result[prop.Name] = value;
            }
            return result;
        }

        private static LegacyValue? ParseEntry(string? type, JToken? v)
        {
            if (v == null) return null;
            try
            {
                switch (type)
                {
                    case "bool": return LegacyValue.OfBool((bool)v);
                    case "int": return LegacyValue.OfInt((long)v);
                    case "double": return LegacyValue.OfDouble((double)v);
                    case "date": return LegacyValue.OfDate((double)v);
                    case "string":
                    case "data": return LegacyValue.OfString((string)v!);
                    case "bytes": return LegacyValue.OfBytes(System.Convert.FromBase64String((string)v!));
                    case "array":
                        if (!(v is JArray array)) return null;
                        if (array.All(x => x.Type == JTokenType.Integer)) return LegacyValue.OfIntList(array.Select(x => (long)x));
                        if (array.All(x => x.Type == JTokenType.String)) return LegacyValue.OfStringList(array.Select(x => (string)x!));
                        return null;
                    default: return null;
                }
            }
            catch (Exception e) when (e is FormatException || e is ArgumentException || e is InvalidCastException || e is OverflowException)
            {
                return null;
            }
        }

        /// Garde les clés du jeu ; les autres sont listées comme ignorées.
        public static LegacyConversion Convert(IReadOnlyDictionary<string, LegacyValue> defaults)
        {
            var conversion = new LegacyConversion("ios");
            foreach (var pair in defaults)
            {
                if (LegacySchema.TypeOf(pair.Key) == null)
                {
                    conversion.Ignore(pair.Key, "clé étrangère au jeu (SDK, PlayerPrefs)");
                    continue;
                }
                // `NSDate` (premier lancement, dernière interstitielle) : déjà en
                // secondes Swift, écrites en double comme le fait Core.
                conversion.Values[pair.Key] = pair.Value;
            }
            return conversion;
        }

        public static LegacyConversion ConvertPluginJson(string json) => Convert(ParsePluginJson(json));
    }
}
