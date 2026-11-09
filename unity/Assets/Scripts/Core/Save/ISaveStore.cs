#nullable enable
using System.Collections.Generic;

namespace TenGo.Core
{
    /// Magasin clé-valeur persistant, équivalent de `UserDefaults.standard`.
    /// Les lectures d'une clé absente renvoient la valeur par défaut, comme
    /// `integer(forKey:)` (0), `bool(forKey:)` (false) ou `string(forKey:)` (nil).
    /// Les clés sont celles d'iOS (`SaveKeys`). L'implémentation fichier JSON
    /// vit dans la couche jeu ; `MemorySaveStore` sert aux tests.
    public interface ISaveStore
    {
        bool Has(string key);
        void Remove(string key);

        int GetInt(string key, int defaultValue = 0);
        void SetInt(string key, int value);

        long GetLong(string key, long defaultValue = 0);
        void SetLong(string key, long value);

        double GetDouble(string key, double defaultValue = 0);
        void SetDouble(string key, double value);

        bool GetBool(string key, bool defaultValue = false);
        void SetBool(string key, bool value);

        string? GetString(string key);
        void SetString(string key, string value);

        /// Liste ordonnée de chaînes (`stringArray(forKey:)`), aussi utilisée
        /// pour les ensembles (thèmes possédés, succès débloqués).
        IReadOnlyList<string>? GetStringList(string key);
        void SetStringList(string key, IEnumerable<string> values);

        /// Liste ordonnée d'entiers (`array(forKey:) as? [Int]`, meilleurs scores).
        IReadOnlyList<int>? GetIntList(string key);
        void SetIntList(string key, IEnumerable<int> values);
    }
}
