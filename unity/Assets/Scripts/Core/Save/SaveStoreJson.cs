#nullable enable
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TenGo.Core
{
    /// Format du fichier de sauvegarde Unity : `{"format":1,"values":{clé: valeur}}`,
    /// valeurs JSON natives (booléen, entier, flottant, chaîne, tableau
    /// d'entiers ou de chaînes). Les flottants gardent leur décimale (`815094000.0`)
    /// pour être relus en double.
    public static class SaveStoreJson
    {
        public const int Format = 1;

        public static string ToJson(MemorySaveStore store)
        {
            var values = new JObject();
            foreach (var pair in store.Values.OrderBy(p => p.Key, System.StringComparer.Ordinal))
            {
                switch (pair.Value)
                {
                    case List<string> strings: values[pair.Key] = new JArray(strings); break;
                    case List<int> ints: values[pair.Key] = new JArray(ints); break;
                    default: values[pair.Key] = new JValue(pair.Value); break;
                }
            }
            return new JObject { ["format"] = Format, ["values"] = values }.ToString(Formatting.Indented);
        }

        /// Relit un fichier. Lève `JsonException` (ou `FormatException`) s'il
        /// est illisible ou d'un format inconnu ; une valeur d'un type
        /// inattendu est ignorée.
        public static MemorySaveStore FromJson(string json)
        {
            JObject root = JObject.Parse(json);
            if ((int?)root["format"] != Format || !(root["values"] is JObject values))
                throw new JsonException("sauvegarde : format inconnu");
            var store = new MemorySaveStore();
            foreach (JProperty prop in values.Properties())
            {
                JToken v = prop.Value;
                switch (v.Type)
                {
                    case JTokenType.Boolean: store.SetBool(prop.Name, (bool)v); break;
                    case JTokenType.Integer:
                        long l = (long)v;
                        if (l >= int.MinValue && l <= int.MaxValue) store.SetInt(prop.Name, (int)l);
                        else store.SetLong(prop.Name, l);
                        break;
                    case JTokenType.Float: store.SetDouble(prop.Name, (double)v); break;
                    case JTokenType.String: store.SetString(prop.Name, (string)v!); break;
                    case JTokenType.Array:
                        var array = (JArray)v;
                        if (array.Count > 0 && array.All(x => x.Type == JTokenType.Integer))
                            store.SetIntList(prop.Name, array.Select(x => (int)x));
                        else if (array.All(x => x.Type == JTokenType.String))
                            store.SetStringList(prop.Name, array.Select(x => (string)x!));
                        break;
                }
            }
            return store;
        }
    }
}
