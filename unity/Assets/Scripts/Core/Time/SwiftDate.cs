#nullable enable
using System;

namespace TenGo.Core
{
    /// Conversions vers la représentation des dates Swift : `Date` est un nombre
    /// de secondes depuis le 1er janvier 2001 UTC (`timeIntervalSinceReferenceDate`),
    /// et c'est ce nombre que `JSONEncoder` écrit par défaut. Les sauvegardes
    /// reprennent ce format pour rester lisibles par la reprise des données iOS.
    public static class SwiftDate
    {
        /// 2001-01-01T00:00:00Z en secondes Unix.
        public const long ReferenceUnixSeconds = 978_307_200L;

        public static readonly DateTimeOffset Reference = DateTimeOffset.FromUnixTimeSeconds(ReferenceUnixSeconds);

        /// Instant → secondes depuis la date de référence Swift.
        public static double ToReferenceSeconds(DateTimeOffset instant) => (instant - Reference).TotalSeconds;

        /// Secondes depuis la date de référence Swift → instant.
        public static DateTimeOffset FromReferenceSeconds(double seconds) => Reference.AddTicks((long)Math.Round(seconds * TimeSpan.TicksPerSecond));

        /// Millisecondes Unix → secondes de référence Swift.
        public static double UnixMsToReferenceSeconds(long unixMs) => unixMs / 1000.0 - ReferenceUnixSeconds;

        /// Secondes de référence Swift → millisecondes Unix (arrondi au plus proche).
        public static long ReferenceSecondsToUnixMs(double seconds) => (long)Math.Round((seconds + ReferenceUnixSeconds) * 1000.0);
    }
}
