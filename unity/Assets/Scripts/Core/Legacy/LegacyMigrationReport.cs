#nullable enable
using System.Collections.Generic;
using System.Text;

namespace TenGo.Core
{
    /// Bilan d'un passage de `LegacyMigration`. Ne contient que des noms de
    /// clés et des raisons, jamais de valeurs (pas de données personnelles
    /// dans le journal).
    public sealed class LegacyMigrationReport
    {
        public LegacyMigrationReport(LegacyMigrationStatus status, string source)
        {
            Status = status;
            Source = source;
        }

        public LegacyMigrationStatus Status { get; }

        public string Source { get; }

        /// Clés iOS écrites dans le magasin Unity.
        public List<string> Imported { get; } = new List<string>();

        /// Clés iOS déjà présentes côté Unity, laissées intactes.
        public List<string> Kept { get; } = new List<string>();

        /// Clés (source ou cible) laissées de côté → raison.
        public SortedDictionary<string, string> Ignored { get; } = new SortedDictionary<string, string>(System.StringComparer.Ordinal);

        /// Message d'erreur (type d'exception), en cas d'échec de lecture.
        public string? Error { get; set; }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append("[Legacy] ").Append(Status).Append(" (").Append(Source).Append(')');
            if (Error != null) sb.Append(" erreur : ").Append(Error);
            if (Imported.Count > 0) sb.Append(" ; reprises (").Append(Imported.Count).Append(") : ").Append(string.Join(", ", Imported));
            if (Kept.Count > 0) sb.Append(" ; conservées côté Unity : ").Append(string.Join(", ", Kept));
            if (Ignored.Count > 0)
            {
                sb.Append(" ; ignorées : ");
                bool first = true;
                foreach (var pair in Ignored)
                {
                    if (!first) sb.Append(", ");
                    sb.Append(pair.Key).Append(" (").Append(pair.Value).Append(')');
                    first = false;
                }
            }
            return sb.ToString();
        }
    }
}
