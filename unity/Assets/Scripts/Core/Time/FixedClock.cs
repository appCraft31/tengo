#nullable enable
using System;

namespace TenGo.Core
{
    /// Horloge figée et réglable (tests, captures, outils).
    public sealed class FixedClock : IClock
    {
        public FixedClock(DateTimeOffset now, TimeZoneInfo? zone = null)
        {
            Now = now;
            Zone = zone ?? TimeZoneInfo.Utc;
        }

        public DateTimeOffset Now { get; set; }

        public TimeZoneInfo Zone { get; set; }

        /// Avance l'horloge.
        public void Advance(TimeSpan delta) => Now = Now + delta;
    }
}
