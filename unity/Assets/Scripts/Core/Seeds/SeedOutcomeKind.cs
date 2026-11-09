#nullable enable
namespace TenGo.Core
{
    /// Ce que la fin de partie dit de la graine (`SeedGarden.PlantOutcome`).
    public enum SeedOutcomeKind
    {
        /// Nouvelle graine plantée.
        Planted,
        /// Une graine poussait déjà : la chaîne plus longue la renforce.
        Strengthened,
        /// Une graine pousse déjà, au moins aussi forte.
        StillGrowing,
        /// Une fleur attend d'être utilisée.
        FlowerWaiting,
        /// Chaîne trop courte pour planter.
        TooShort,
    }
}
