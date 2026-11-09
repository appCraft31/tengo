using System;
using System.IO;
using TenGo.Core;
using UnityEngine;

namespace TenGo.Platform.Legacy
{
    /// Point d'entrée de la reprise des données natives, à appeler une fois au
    /// démarrage (étape 5), juste après `JsonFileSaveStore.OpenDefault()` et
    /// avant toute lecture par les dépôts.
    public static class LegacyBootstrap
    {
        /// Éditeur : dossier de fixtures facultatif, au format d'un appareil
        /// (`<dossier>/files/datastore/…`, `<dossier>/shared_prefs/…`) ou
        /// `<dossier>/ios.json` (réponse du greffon iOS).
        public const string EditorFixtureEnv = "TENGO_LEGACY_FIXTURE";

        public static LegacyMigrationReport Run(JsonFileSaveStore store)
        {
            LegacyMigrationReport report = LegacyMigration.Run(store, CreateSource(), Application.version, Debug.Log);
            store.Flush();
            return report;
        }

        /// Source de la plateforme courante (null : rien à reprendre).
        public static ILegacySource CreateSource()
        {
#if UNITY_EDITOR
            string dir = Environment.GetEnvironmentVariable(EditorFixtureEnv);
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return null;
            string iosJson = Path.Combine(dir, "ios.json");
            if (File.Exists(iosJson)) return new IosJsonLegacySource(_ => File.ReadAllText(iosJson));
            return new AndroidFilesLegacySource(Path.Combine(dir, "files"), Path.Combine(dir, "shared_prefs"), TimeZoneInfo.Local);
#elif UNITY_IOS
            return new IosJsonLegacySource(IosLegacyDefaults.Read);
#elif UNITY_ANDROID
            // Résolution JNI dans Read() : une erreur y compte comme un échec de
            // lecture (nouvel essai au prochain lancement), pas comme « aucune donnée ».
            return new DeferredSource("android", () =>
            {
                var paths = AndroidLegacyPaths.Resolve();
                if (paths == null) throw new InvalidOperationException("dossiers Android introuvables");
                return new AndroidFilesLegacySource(paths.Value.filesDir, paths.Value.sharedPrefsDir, TimeZoneInfo.Local).Read();
            });
#else
            return null;
#endif
        }

        /// Source dont la préparation elle-même peut échouer.
        private sealed class DeferredSource : ILegacySource
        {
            private readonly Func<LegacyConversion> _read;

            public DeferredSource(string name, Func<LegacyConversion> read)
            {
                Name = name;
                _read = read;
            }

            public string Name { get; }

            public LegacyConversion Read() => _read();
        }
    }
}
