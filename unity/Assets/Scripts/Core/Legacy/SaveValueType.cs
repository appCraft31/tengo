#nullable enable
namespace TenGo.Core
{
    /// Type attendu d'une clé de sauvegarde, tel que l'écrit iOS (AppConfig.swift).
    public enum SaveValueType
    {
        Bool,
        Int,
        /// `Double` ou `Date` Swift : secondes de référence Swift.
        Double,
        String,
        StringList,
        IntList,
        /// `Data` contenant du JSON (`JSONEncoder`) : stocké en chaîne.
        Json,
    }
}
