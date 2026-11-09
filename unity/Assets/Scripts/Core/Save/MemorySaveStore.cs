#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace TenGo.Core
{
    /// Magasin en mémoire (tests, et base de l'implémentation fichier). Les
    /// lectures sont tolérantes comme `UserDefaults` : un booléen lu comme
    /// entier vaut 0/1, un nombre stocké en chaîne est relu.
    public class MemorySaveStore : ISaveStore
    {
        private readonly Dictionary<string, object> _values = new Dictionary<string, object>();

        /// Contenu brut (sérialisation, diagnostic).
        public IReadOnlyDictionary<string, object> Values => _values;

        public bool Has(string key) => _values.ContainsKey(key);

        public void Remove(string key) => _values.Remove(key);

        /// Écrit une valeur brute (int, long, double, bool, string, liste).
        protected void SetRaw(string key, object value) => _values[key] = value;

        public int GetInt(string key, int defaultValue = 0)
        {
            if (!_values.TryGetValue(key, out object? v)) return defaultValue;
            switch (v)
            {
                case int i: return i;
                case long l: return (int)l;
                case double d: return (int)d;
                case bool b: return b ? 1 : 0;
                case string s when int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int p): return p;
                default: return defaultValue;
            }
        }

        public void SetInt(string key, int value) => _values[key] = value;

        public long GetLong(string key, long defaultValue = 0)
        {
            if (!_values.TryGetValue(key, out object? v)) return defaultValue;
            switch (v)
            {
                case int i: return i;
                case long l: return l;
                case double d: return (long)d;
                case bool b: return b ? 1 : 0;
                case string s when long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out long p): return p;
                default: return defaultValue;
            }
        }

        public void SetLong(string key, long value) => _values[key] = value;

        public double GetDouble(string key, double defaultValue = 0)
        {
            if (!_values.TryGetValue(key, out object? v)) return defaultValue;
            switch (v)
            {
                case int i: return i;
                case long l: return l;
                case double d: return d;
                case bool b: return b ? 1 : 0;
                case string s when double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double p): return p;
                default: return defaultValue;
            }
        }

        public void SetDouble(string key, double value) => _values[key] = value;

        public bool GetBool(string key, bool defaultValue = false)
        {
            if (!_values.TryGetValue(key, out object? v)) return defaultValue;
            switch (v)
            {
                case bool b: return b;
                case int i: return i != 0;
                case long l: return l != 0;
                case double d: return d != 0;
                case string s: return s == "1" || s.Equals("true", StringComparison.OrdinalIgnoreCase) || s.Equals("yes", StringComparison.OrdinalIgnoreCase);
                default: return defaultValue;
            }
        }

        public void SetBool(string key, bool value) => _values[key] = value;

        public string? GetString(string key)
        {
            if (!_values.TryGetValue(key, out object? v)) return null;
            switch (v)
            {
                case string s: return s;
                case int i: return i.ToString(CultureInfo.InvariantCulture);
                case long l: return l.ToString(CultureInfo.InvariantCulture);
                case double d: return d.ToString("R", CultureInfo.InvariantCulture);
                default: return null;
            }
        }

        public void SetString(string key, string value) => _values[key] = value;

        public IReadOnlyList<string>? GetStringList(string key) =>
            _values.TryGetValue(key, out object? v) && v is List<string> list ? list.ToList() : null;

        public void SetStringList(string key, IEnumerable<string> values) => _values[key] = values.ToList();

        public IReadOnlyList<int>? GetIntList(string key) =>
            _values.TryGetValue(key, out object? v) && v is List<int> list ? list.ToList() : null;

        public void SetIntList(string key, IEnumerable<int> values) => _values[key] = values.ToList();
    }
}
