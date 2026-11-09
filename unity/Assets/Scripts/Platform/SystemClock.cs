using System;
using TenGo.Core;

namespace TenGo.Platform
{
    /// Horloge réelle de l'appareil, fuseau local (`Date()` et `Calendar.current`).
    public sealed class SystemClock : IClock
    {
        public static readonly SystemClock Instance = new();
        public DateTimeOffset Now => DateTimeOffset.Now;
        public TimeZoneInfo Zone => TimeZoneInfo.Local;
    }
}
