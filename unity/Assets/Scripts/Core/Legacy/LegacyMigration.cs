#nullable enable
using System;
using System.Linq;

namespace TenGo.Core
{
    /// Reprise, au premier lancement de la 4.0.0 Unity, des données de l'app
    /// native qu'elle remplace (iOS ou Android 3.3.0, mêmes identifiants).
    ///
    /// Règles :
    ///  - une seule fois : marqueur `unity_migratedFromNative` (+ version et
    ///    source), posé aussi quand il n'y a rien à reprendre ;
    ///  - les données natives ne sont jamais modifiées ni effacées (retour
    ///    arrière possible vers la 3.3.0) ;
    ///  - une valeur déjà présente dans le magasin Unity n'est jamais remplacée ;
    ///  - le bilan (`LegacyMigrationReport`) ne cite que des noms de clés.
    ///
    /// À appeler au démarrage, AVANT tout dépôt susceptible d'écrire une valeur
    /// par défaut (ex. `FirstRunState.Bootstrap`), puis vider le magasin sur disque.
    public static class LegacyMigration
    {
        public const string MarkerKey = "unity_migratedFromNative";
        public const string VersionKey = "unity_migratedFromNativeVersion";
        public const string SourceKey = "unity_migratedFromNativeSource";
        public const string AttemptsKey = "unity_migrationAttempts";

        /// Au-delà, une lecture qui échoue toujours est abandonnée.
        public const int MaxAttempts = 3;

        public static bool IsDone(ISaveStore store) => store.GetBool(MarkerKey);

        /// `appVersion` : version de l'app Unity qui a fait la reprise (« 4.0.0 »).
        public static LegacyMigrationReport Run(ISaveStore store, ILegacySource? source, string appVersion, Action<string>? log = null)
        {
            string sourceName = source?.Name ?? "aucune";
            if (IsDone(store))
                return new LegacyMigrationReport(LegacyMigrationStatus.AlreadyDone, store.GetString(SourceKey) ?? sourceName);

            LegacyConversion? conversion;
            try
            {
                conversion = source?.Read();
            }
            catch (Exception e)
            {
                int attempts = store.GetInt(AttemptsKey) + 1;
                bool giveUp = attempts >= MaxAttempts;
                var failed = new LegacyMigrationReport(giveUp ? LegacyMigrationStatus.GaveUp : LegacyMigrationStatus.Failed, sourceName)
                {
                    Error = e.GetType().Name,
                };
                if (giveUp) MarkDone(store, sourceName, appVersion);
                else store.SetInt(AttemptsKey, attempts);
                log?.Invoke(failed.ToString());
                return failed;
            }

            if (conversion == null || conversion.IsEmpty)
            {
                var none = new LegacyMigrationReport(LegacyMigrationStatus.NoNativeData, sourceName);
                if (conversion != null) foreach (var i in conversion.Ignored) none.Ignored[i.Key] = i.Value;
                MarkDone(store, sourceName, appVersion);
                log?.Invoke(none.ToString());
                return none;
            }

            var report = new LegacyMigrationReport(LegacyMigrationStatus.Imported, conversion.Source);
            foreach (var i in conversion.Ignored) report.Ignored[i.Key] = i.Value;
            foreach (var pair in conversion.Values.OrderBy(p => p.Key, StringComparer.Ordinal))
            {
                SaveValueType? type = LegacySchema.TypeOf(pair.Key);
                if (type == null)
                {
                    report.Ignored[pair.Key] = "clé hors schéma";
                    continue;
                }
                if (store.Has(pair.Key))
                {
                    report.Kept.Add(pair.Key);
                    continue;
                }
                LegacyValue? value = LegacySchema.Normalize(type.Value, pair.Value, out string? reason);
                if (value == null)
                {
                    report.Ignored[pair.Key] = reason ?? "valeur inutilisable";
                    continue;
                }
                LegacySchema.Write(store, pair.Key, type.Value, value);
                report.Imported.Add(pair.Key);
            }
            MarkDone(store, conversion.Source, appVersion);
            log?.Invoke(report.ToString());
            return report;
        }

        private static void MarkDone(ISaveStore store, string source, string appVersion)
        {
            store.SetBool(MarkerKey, true);
            store.SetString(VersionKey, appVersion);
            store.SetString(SourceKey, source);
            store.Remove(AttemptsKey);
        }
    }
}
