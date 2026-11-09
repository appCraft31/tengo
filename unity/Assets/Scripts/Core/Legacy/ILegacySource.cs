#nullable enable
namespace TenGo.Core
{
    /// Accès en lecture seule aux données de l'app native remplacée. La couche
    /// jeu fournit la lecture des UserDefaults (iOS) ou des fichiers internes
    /// (Android) ; les tests, des fixtures.
    public interface ILegacySource
    {
        /// « ios », « android » ou « fixture ».
        string Name { get; }

        /// Données ramenées au schéma iOS, ou null s'il n'y a aucune donnée
        /// native (nouvelle installation). Peut lever une exception si les
        /// données existent mais sont illisibles. Ne doit JAMAIS modifier ni
        /// effacer les données natives (retour arrière possible vers la 3.3.0).
        LegacyConversion? Read();
    }
}
