#nullable enable
namespace TenGo.Core
{
    public enum LegacyMigrationStatus
    {
        /// Déjà faite lors d'un lancement précédent : rien n'a été touché.
        AlreadyDone,
        /// Données natives reprises.
        Imported,
        /// Aucune donnée native (nouvelle installation) : marqueur posé.
        NoNativeData,
        /// Lecture impossible : marqueur non posé, nouvel essai au prochain lancement.
        Failed,
        /// Échecs répétés : on renonce (marqueur posé) pour ne pas réessayer sans fin.
        GaveUp,
    }
}
