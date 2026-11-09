#nullable enable
using System.Collections.Generic;

namespace TenGo.Core
{
    /// Données d'une app native ramenées au schéma iOS (`LegacySchema`), prêtes
    /// à être écrites, avec la liste des clés sources laissées de côté.
    public sealed class LegacyConversion
    {
        public LegacyConversion(string source)
        {
            Source = source;
        }

        /// « ios » ou « android ».
        public string Source { get; }

        /// Clé iOS → valeur (pas encore normalisée).
        public Dictionary<string, LegacyValue> Values { get; } = new Dictionary<string, LegacyValue>();

        /// Clé source ignorée → raison (sans la valeur).
        public SortedDictionary<string, string> Ignored { get; } = new SortedDictionary<string, string>(System.StringComparer.Ordinal);

        public bool IsEmpty => Values.Count == 0;

        public void Ignore(string key, string reason) => Ignored[key] = reason;
    }
}
