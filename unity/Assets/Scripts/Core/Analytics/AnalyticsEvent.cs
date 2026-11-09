#nullable enable
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace TenGo.Core
{
    /// Événement analytics prêt à partir : nom et paramètres dans l'ordre
    /// d'AnalyticsService.swift. Les valeurs sont `long` (Int Swift), `double`,
    /// `string` ou une liste d'objets (`items` de `purchase`), les seuls types
    /// que Firebase accepte.
    public sealed class AnalyticsEvent
    {
        public readonly string Name;
        public readonly List<KeyValuePair<string, object>> Parameters = new();

        public AnalyticsEvent(string name) { Name = name; }

        public AnalyticsEvent Add(string key, string value) { Parameters.Add(new(key, value)); return this; }
        public AnalyticsEvent Add(string key, long value) { Parameters.Add(new(key, value)); return this; }
        public AnalyticsEvent Add(string key, double value) { Parameters.Add(new(key, value)); return this; }
        public AnalyticsEvent Add(string key, IReadOnlyList<IReadOnlyDictionary<string, object>> items) { Parameters.Add(new(key, items)); return this; }

        public object? this[string key] => Parameters.FirstOrDefault(p => p.Key == key).Value;
        public bool Has(string key) => Parameters.Any(p => p.Key == key);

        /// `nom k=v k=v` (journal, tests) ; décimaux avec un point.
        public override string ToString()
        {
            if (Parameters.Count == 0) return Name;
            return Name + " " + string.Join(" ", Parameters.Select(p => p.Key + "=" + Format(p.Value)));
        }

        static string Format(object v) => v switch
        {
            double d => d.ToString("0.######", CultureInfo.InvariantCulture),
            IReadOnlyList<IReadOnlyDictionary<string, object>> items =>
                "[" + string.Join(",", items.Select(i => "{" + string.Join(",", i.Select(kv => kv.Key + ":" + kv.Value)) + "}")) + "]",
            _ => v.ToString() ?? "",
        };
    }
}
