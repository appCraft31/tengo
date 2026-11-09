#nullable enable
using System;
using System.Collections.Generic;
using System.Text;

namespace TenGo.Core
{
    /// Décodeur minimal du fichier DataStore Preferences d'Android
    /// (`files/datastore/tengo.preferences_pb`). Schéma `preferences.proto`
    /// d'androidx.datastore (vérifié sur `datastore-preferences-proto` 1.1.7) :
    ///
    ///     message PreferenceMap { map<string, Value> preferences = 1; }
    ///     message Value { oneof ValueCase {
    ///         bool boolean = 1; float float = 2; int32 integer = 3;
    ///         int64 long = 4; string string = 5; StringSet string_set = 6;
    ///         double double = 7; bytes bytes = 8; } }
    ///     message StringSet { repeated string strings = 1; }
    ///
    /// Une entrée de map est un message `{ string key = 1; Value value = 2; }`.
    /// Les champs inconnus sont sautés selon leur type de fil.
    public static class DataStorePreferencesDecoder
    {
        private const int WireVarint = 0;
        private const int WireFixed64 = 1;
        private const int WireLengthDelimited = 2;
        private const int WireFixed32 = 5;

        /// Clé → valeur typée. Lève `FormatException` si le fichier est tronqué
        /// ou mal formé. Une valeur sans cas (`VALUE_NOT_SET`) est omise.
        public static Dictionary<string, LegacyValue> Decode(byte[] data)
        {
            var result = new Dictionary<string, LegacyValue>();
            var reader = new Reader(data, 0, data.Length);
            while (!reader.AtEnd)
            {
                var (field, wire) = reader.ReadTag();
                if (field == 1 && wire == WireLengthDelimited)
                {
                    var (start, length) = reader.ReadLengthDelimited();
                    var (key, value) = DecodeEntry(data, start, length);
                    if (value != null) result[key] = value; // dernière occurrence gagnante, comme protobuf
                }
                else
                {
                    reader.Skip(wire);
                }
            }
            return result;
        }

        private static (string key, LegacyValue? value) DecodeEntry(byte[] data, int offset, int length)
        {
            string key = "";
            LegacyValue? value = null;
            var reader = new Reader(data, offset, length);
            while (!reader.AtEnd)
            {
                var (field, wire) = reader.ReadTag();
                if (field == 1 && wire == WireLengthDelimited)
                {
                    var (s, l) = reader.ReadLengthDelimited();
                    key = Utf8(data, s, l);
                }
                else if (field == 2 && wire == WireLengthDelimited)
                {
                    var (s, l) = reader.ReadLengthDelimited();
                    value = DecodeValue(data, s, l);
                }
                else
                {
                    reader.Skip(wire);
                }
            }
            return (key, value);
        }

        private static LegacyValue? DecodeValue(byte[] data, int offset, int length)
        {
            LegacyValue? value = null;
            var reader = new Reader(data, offset, length);
            while (!reader.AtEnd)
            {
                var (field, wire) = reader.ReadTag();
                switch (field)
                {
                    case 1 when wire == WireVarint:
                        value = LegacyValue.OfBool(reader.ReadVarint() != 0);
                        break;
                    case 2 when wire == WireFixed32:
                        value = LegacyValue.OfFloat(BitConverter.Int32BitsToSingle((int)reader.ReadFixed32()));
                        break;
                    case 3 when wire == WireVarint:
                        // int32 : les négatifs sont étendus sur 10 octets, on tronque à 32 bits.
                        value = LegacyValue.OfInt(unchecked((int)reader.ReadVarint()));
                        break;
                    case 4 when wire == WireVarint:
                        value = LegacyValue.OfLong(unchecked((long)reader.ReadVarint()));
                        break;
                    case 5 when wire == WireLengthDelimited:
                    {
                        var (s, l) = reader.ReadLengthDelimited();
                        value = LegacyValue.OfString(Utf8(data, s, l));
                        break;
                    }
                    case 6 when wire == WireLengthDelimited:
                    {
                        var (s, l) = reader.ReadLengthDelimited();
                        value = LegacyValue.OfStringSet(DecodeStringSet(data, s, l));
                        break;
                    }
                    case 7 when wire == WireFixed64:
                        value = LegacyValue.OfDouble(BitConverter.Int64BitsToDouble(unchecked((long)reader.ReadFixed64())));
                        break;
                    case 8 when wire == WireLengthDelimited:
                    {
                        var (s, l) = reader.ReadLengthDelimited();
                        var bytes = new byte[l];
                        Array.Copy(data, s, bytes, 0, l);
                        value = LegacyValue.OfBytes(bytes);
                        break;
                    }
                    default:
                        reader.Skip(wire);
                        break;
                }
            }
            return value;
        }

        private static List<string> DecodeStringSet(byte[] data, int offset, int length)
        {
            var strings = new List<string>();
            var reader = new Reader(data, offset, length);
            while (!reader.AtEnd)
            {
                var (field, wire) = reader.ReadTag();
                if (field == 1 && wire == WireLengthDelimited)
                {
                    var (s, l) = reader.ReadLengthDelimited();
                    strings.Add(Utf8(data, s, l));
                }
                else
                {
                    reader.Skip(wire);
                }
            }
            return strings;
        }

        private static string Utf8(byte[] data, int offset, int length) =>
            new UTF8Encoding(false, true).GetString(data, offset, length);

        /// Lecteur d'une tranche de tampon, borné à la sous-structure en cours.
        private struct Reader
        {
            private readonly byte[] _data;
            private readonly int _end;
            private int _pos;

            public Reader(byte[] data, int offset, int length)
            {
                if (offset < 0 || length < 0 || offset + length > data.Length) throw new FormatException("protobuf : tranche hors du fichier");
                _data = data;
                _pos = offset;
                _end = offset + length;
            }

            public bool AtEnd => _pos >= _end;

            public (int field, int wire) ReadTag()
            {
                ulong tag = ReadVarint();
                int field = (int)(tag >> 3);
                if (field <= 0) throw new FormatException("protobuf : numéro de champ invalide");
                return (field, (int)(tag & 7));
            }

            public ulong ReadVarint()
            {
                ulong result = 0;
                for (int shift = 0; shift < 64; shift += 7)
                {
                    if (_pos >= _end) throw new FormatException("protobuf : varint tronqué");
                    byte b = _data[_pos++];
                    result |= (ulong)(b & 0x7F) << shift;
                    if ((b & 0x80) == 0) return result;
                }
                throw new FormatException("protobuf : varint trop long");
            }

            public uint ReadFixed32()
            {
                Need(4);
                uint v = (uint)(_data[_pos] | _data[_pos + 1] << 8 | _data[_pos + 2] << 16 | _data[_pos + 3] << 24);
                _pos += 4;
                return v;
            }

            public ulong ReadFixed64()
            {
                ulong lo = ReadFixed32();
                ulong hi = ReadFixed32();
                return lo | hi << 32;
            }

            public (int start, int length) ReadLengthDelimited()
            {
                ulong length = ReadVarint();
                if (length > int.MaxValue) throw new FormatException("protobuf : longueur invalide");
                Need((int)length);
                int start = _pos;
                _pos += (int)length;
                return (start, (int)length);
            }

            public void Skip(int wire)
            {
                switch (wire)
                {
                    case WireVarint: ReadVarint(); break;
                    case WireFixed64: Need(8); _pos += 8; break;
                    case WireLengthDelimited: ReadLengthDelimited(); break;
                    case WireFixed32: Need(4); _pos += 4; break;
                    default: throw new FormatException("protobuf : type de fil " + wire + " non géré");
                }
            }

            private void Need(int count)
            {
                if (count < 0 || _pos + count > _end) throw new FormatException("protobuf : données tronquées");
            }
        }
    }
}
