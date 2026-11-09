#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

namespace TenGo.Core
{
    /// Lecture d'un fichier SharedPreferences Android (`shared_prefs/<nom>.xml`,
    /// écrit par `XmlUtils.writeMapXml`) :
    ///
    ///     <map>
    ///       <int name="k" value="1" />  <long …/>  <float …/>  <boolean …/>
    ///       <string name="k">texte &amp; entités</string>
    ///       <set name="k"><string>a</string>…</set>
    ///       <null name="k" />
    ///     </map>
    ///
    /// Les entités XML sont résolues par le parseur. Une entrée `<null>` ou
    /// illisible est omise.
    public static class SharedPreferencesXmlDecoder
    {
        /// Lève `FormatException` si le XML est illisible ou sans `<map>`.
        public static Dictionary<string, LegacyValue> Decode(string xml)
        {
            XDocument doc;
            try
            {
                doc = XDocument.Parse(xml);
            }
            catch (XmlException e)
            {
                throw new FormatException("SharedPreferences : XML illisible (" + e.Message + ")", e);
            }
            if (doc.Root == null || doc.Root.Name.LocalName != "map") throw new FormatException("SharedPreferences : racine <map> absente");

            var result = new Dictionary<string, LegacyValue>();
            foreach (XElement e in doc.Root.Elements())
            {
                string? name = (string?)e.Attribute("name");
                if (name == null) continue;
                LegacyValue? value = Parse(e);
                if (value != null) result[name] = value;
            }
            return result;
        }

        private static LegacyValue? Parse(XElement e)
        {
            string? raw = (string?)e.Attribute("value");
            var inv = CultureInfo.InvariantCulture;
            switch (e.Name.LocalName)
            {
                case "int":
                    return int.TryParse(raw, NumberStyles.Integer, inv, out int i) ? LegacyValue.OfInt(i) : null;
                case "long":
                    return long.TryParse(raw, NumberStyles.Integer, inv, out long l) ? LegacyValue.OfLong(l) : null;
                case "float":
                    return float.TryParse(raw, NumberStyles.Float, inv, out float f) ? LegacyValue.OfFloat(f) : null;
                case "boolean":
                    return raw == "true" ? LegacyValue.OfBool(true) : raw == "false" ? LegacyValue.OfBool(false) : null;
                case "string":
                    return LegacyValue.OfString(e.Value);
                case "set":
                    return LegacyValue.OfStringSet(e.Elements().Where(s => s.Name.LocalName == "string").Select(s => s.Value));
                default:
                    return null; // <null>, ou balise inconnue
            }
        }
    }
}
