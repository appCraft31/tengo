#nullable enable
namespace TenGo.Core
{
    /// Type d'une valeur lue dans les données des apps natives (UserDefaults
    /// iOS, DataStore Preferences ou SharedPreferences Android).
    public enum LegacyValueKind
    {
        Bool,
        /// Entier (int32 Android, NSNumber entier iOS) ; porté sur 64 bits.
        Int,
        /// Entier 64 bits (`longPreferencesKey`, `<long>` des SharedPreferences).
        Long,
        Float,
        Double,
        String,
        /// Ensemble de chaînes (`stringSetPreferencesKey`, `<set>`), sans ordre.
        StringSet,
        /// Tableau ordonné de chaînes (NSArray de NSString).
        StringList,
        /// Tableau ordonné d'entiers (NSArray de NSNumber).
        IntList,
        /// Octets bruts (NSData non UTF-8, `bytes` du DataStore).
        Bytes,
        /// Date Swift (`NSDate`), en secondes depuis le 1er janvier 2001.
        Date,
    }
}
