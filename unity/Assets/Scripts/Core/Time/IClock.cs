#nullable enable
using System;

namespace TenGo.Core
{
    /// Horloge injectée : Core ne lit jamais l'heure système lui-même. La couche
    /// jeu fournit l'horloge réelle, les tests une horloge figée.
    public interface IClock
    {
        /// Instant présent.
        DateTimeOffset Now { get; }

        /// Fuseau du joueur (« heure locale » des règles : bascule de 5 h,
        /// jour de série, quota journalier des pubs).
        TimeZoneInfo Zone { get; }
    }
}
