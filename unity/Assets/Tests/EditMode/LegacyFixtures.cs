using System;
using System.IO;
using TenGo.Core;

namespace TenGo.Tests
{
    /// Fixtures de la reprise des données natives (`Fixtures/Legacy`) :
    ///  - `android/emulator` : fichiers réels de l'app Android 3.3.0 (variante qa),
    ///    récupérés sur emulator-5554 après des parties ;
    ///  - `android/complete` : DataStore écrit par les classes protobuf de
    ///    DataStore 1.1.7 (toutes les clés de Prefs.kt), XML au format Android ;
    ///  - `ios/lived.json`, `ios/seeded.json` : UserDefaults réels de l'app iOS
    ///    sur simulateur, au format de sortie du greffon `TenGoLegacy.mm`.
    internal static class LegacyFixtures
    {
        public static string Root => Path.Combine(Directory.GetCurrentDirectory(), "Assets", "Tests", "EditMode", "Fixtures", "Legacy");

        public static string Android(string name) => Path.Combine(Root, "android", name);

        public static string FilesDir(string name) => Path.Combine(Android(name), "files");

        public static string SharedPrefsDir(string name) => Path.Combine(Android(name), "shared_prefs");

        public static AndroidLegacyData ReadAndroid(string name) =>
            AndroidLegacyData.ReadFiles(FilesDir(name), SharedPrefsDir(name)) ?? throw new InvalidOperationException("fixture absente : " + name);

        public static AndroidFilesLegacySource AndroidSource(string name, TimeZoneInfo zone) =>
            new AndroidFilesLegacySource(FilesDir(name), SharedPrefsDir(name), zone);

        public static string IosJson(string name) => File.ReadAllText(Path.Combine(Root, "ios", name + ".json"));

        public static IosJsonLegacySource IosSource(string name) => new IosJsonLegacySource(_ => IosJson(name));

        /// Fuseau fixe UTC+1, sans heure d'été (calculs de jour reproductibles).
        public static readonly TimeZoneInfo Paris = TimeZoneInfo.CreateCustomTimeZone("UTC+1", TimeSpan.FromHours(1), "UTC+1", "UTC+1");
    }
}
