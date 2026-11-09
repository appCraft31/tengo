#nullable enable
namespace TenGo.Core
{
    /// Source d'aléa abstraite : équivalent du protocole `RandomNumberGenerator`
    /// de Swift. Permet d'injecter un générateur déterministe (défi du jour,
    /// duel) ou un générateur système (mode normal, fourni par la couche jeu).
    /// Les réductions (bornes, mélange, tirage) sont dans `Rng` et reproduisent
    /// la bibliothèque standard Swift au bit près.
    public interface IRng
    {
        /// Prochain entier 64 bits non signé.
        ulong NextULong();
    }
}
