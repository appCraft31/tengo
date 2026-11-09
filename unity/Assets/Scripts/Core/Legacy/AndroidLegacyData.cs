#nullable enable
using System.Collections.Generic;
using System.IO;

namespace TenGo.Core
{
    /// Données brutes de l'app Android 3.3.0, décodées de ses trois fichiers
    /// internes :
    ///  - `files/datastore/tengo.preferences_pb` (DataStore Preferences, `Prefs.kt`) ;
    ///  - `shared_prefs/tengo_first_run.xml` (`FirstRun.kt`) ;
    ///  - `shared_prefs/tengo_ads.xml` (`AdsManager.kt`).
    public sealed class AndroidLegacyData
    {
        public const string DataStoreRelativePath = "datastore/tengo.preferences_pb";
        public const string FirstRunFile = "tengo_first_run.xml";
        public const string AdsFile = "tengo_ads.xml";

        public AndroidLegacyData(
            IReadOnlyDictionary<string, LegacyValue>? preferences,
            IReadOnlyDictionary<string, LegacyValue>? firstRun,
            IReadOnlyDictionary<string, LegacyValue>? ads)
        {
            Preferences = preferences ?? new Dictionary<string, LegacyValue>();
            FirstRun = firstRun ?? new Dictionary<string, LegacyValue>();
            Ads = ads ?? new Dictionary<string, LegacyValue>();
        }

        public IReadOnlyDictionary<string, LegacyValue> Preferences { get; }
        public IReadOnlyDictionary<string, LegacyValue> FirstRun { get; }
        public IReadOnlyDictionary<string, LegacyValue> Ads { get; }

        public bool IsEmpty => Preferences.Count == 0 && FirstRun.Count == 0 && Ads.Count == 0;

        /// Lit les fichiers depuis le dossier interne `files/` (`Context.getFilesDir()`)
        /// et le dossier `shared_prefs/` (`getDataDir()/shared_prefs`). Un fichier
        /// absent est vide ; un fichier illisible lève `FormatException`
        /// (ou `IOException`). Renvoie null si aucun des trois n'existe.
        public static AndroidLegacyData? ReadFiles(string filesDir, string sharedPrefsDir)
        {
            string pb = Path.Combine(filesDir, DataStoreRelativePath);
            string firstRun = Path.Combine(sharedPrefsDir, FirstRunFile);
            string ads = Path.Combine(sharedPrefsDir, AdsFile);
            if (!File.Exists(pb) && !File.Exists(firstRun) && !File.Exists(ads)) return null;
            return new AndroidLegacyData(
                File.Exists(pb) ? DataStorePreferencesDecoder.Decode(File.ReadAllBytes(pb)) : null,
                File.Exists(firstRun) ? SharedPreferencesXmlDecoder.Decode(File.ReadAllText(firstRun)) : null,
                File.Exists(ads) ? SharedPreferencesXmlDecoder.Decode(File.ReadAllText(ads)) : null);
        }
    }
}
