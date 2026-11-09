#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace TenGo.Core
{
    /// Valeur typée lue dans les données d'une app native. Une seule forme pour
    /// les trois sources (UserDefaults, DataStore, SharedPreferences), que les
    /// importeurs convertissent vers le schéma iOS (`LegacySchema`).
    public sealed class LegacyValue
    {
        private LegacyValue(LegacyValueKind kind)
        {
            Kind = kind;
        }

        public LegacyValueKind Kind { get; }

        public bool Bool { get; private set; }

        /// Valeur des types `Int` et `Long`.
        public long Long { get; private set; }

        /// Valeur des types `Float`, `Double` et `Date` (secondes Swift).
        public double Double { get; private set; }

        public string? String { get; private set; }

        /// Valeurs des types `StringSet` et `StringList`.
        public IReadOnlyList<string> Strings { get; private set; } = Array.Empty<string>();

        /// Valeurs du type `IntList`.
        public IReadOnlyList<long> Longs { get; private set; } = Array.Empty<long>();

        public byte[] Bytes { get; private set; } = Array.Empty<byte>();

        public static LegacyValue OfBool(bool value) => new LegacyValue(LegacyValueKind.Bool) { Bool = value };

        public static LegacyValue OfInt(long value) => new LegacyValue(LegacyValueKind.Int) { Long = value };

        public static LegacyValue OfLong(long value) => new LegacyValue(LegacyValueKind.Long) { Long = value };

        public static LegacyValue OfFloat(float value) => new LegacyValue(LegacyValueKind.Float) { Double = value };

        public static LegacyValue OfDouble(double value) => new LegacyValue(LegacyValueKind.Double) { Double = value };

        public static LegacyValue OfString(string value) => new LegacyValue(LegacyValueKind.String) { String = value };

        public static LegacyValue OfStringSet(IEnumerable<string> values) =>
            new LegacyValue(LegacyValueKind.StringSet) { Strings = values.ToList() };

        public static LegacyValue OfStringList(IEnumerable<string> values) =>
            new LegacyValue(LegacyValueKind.StringList) { Strings = values.ToList() };

        public static LegacyValue OfIntList(IEnumerable<long> values) =>
            new LegacyValue(LegacyValueKind.IntList) { Longs = values.ToList() };

        public static LegacyValue OfBytes(byte[] value) => new LegacyValue(LegacyValueKind.Bytes) { Bytes = value };

        /// Date Swift, en secondes depuis le 1er janvier 2001 UTC.
        public static LegacyValue OfDate(double referenceSeconds) =>
            new LegacyValue(LegacyValueKind.Date) { Double = referenceSeconds };

        public bool IsInteger => Kind == LegacyValueKind.Int || Kind == LegacyValueKind.Long;

        public bool IsNumber => IsInteger || Kind == LegacyValueKind.Float || Kind == LegacyValueKind.Double || Kind == LegacyValueKind.Date;

        public bool IsStrings => Kind == LegacyValueKind.StringSet || Kind == LegacyValueKind.StringList;

        /// Description du type seul, sans la valeur (journal sans données personnelles).
        public override string ToString() => Kind.ToString();

        /// Valeur lisible, pour les messages de test uniquement.
        public string Describe()
        {
            switch (Kind)
            {
                case LegacyValueKind.Bool: return Bool ? "true" : "false";
                case LegacyValueKind.Int:
                case LegacyValueKind.Long: return Long.ToString(CultureInfo.InvariantCulture);
                case LegacyValueKind.Float:
                case LegacyValueKind.Double:
                case LegacyValueKind.Date: return Double.ToString("R", CultureInfo.InvariantCulture);
                case LegacyValueKind.String: return String ?? "";
                case LegacyValueKind.StringSet:
                case LegacyValueKind.StringList: return "[" + string.Join(",", Strings) + "]";
                case LegacyValueKind.IntList: return "[" + string.Join(",", Longs) + "]";
                default: return Bytes.Length + " octets";
            }
        }
    }
}
