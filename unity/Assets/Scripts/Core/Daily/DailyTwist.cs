#nullable enable
namespace TenGo.Core
{
    /// Élément perturbateur du jour, en rotation déterministe (ordre des cas
    /// identique à iOS : il indexe la rotation).
    public enum DailyTwist
    {
        /// Pierres inertes : cassent l'adjacence et bloquent la gravité.
        Obstacles = 0,
        /// Bulles immobiles à la gravité.
        Anchored = 1,
        /// Bulles givrées, débloquées par un chemin adjacent.
        Frozen = 2,
    }
}
