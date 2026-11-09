#nullable enable
using System;

namespace TenGo.Core
{
    /// Clés de jour AAAAMMJJ et jours calendaires locaux, tels que les calcule
    /// le jeu iOS (`Calendar` grégorien dans le fuseau du joueur).
    public static class DayKeys
    {
        /// Heure locale à laquelle une nouvelle « journée de défi » commence.
        public const int ResetHour = 5;

        /// Instant converti en heure locale du fuseau.
        public static DateTimeOffset ToLocal(DateTimeOffset instant, TimeZoneInfo zone) =>
            TimeZoneInfo.ConvertTime(instant, zone);

        /// Clé AAAAMMJJ d'une date.
        public static int Of(DateTime date) => date.Year * 10000 + date.Month * 100 + date.Day;

        /// Clé du défi du jour (et des missions) : la journée bascule à 5 h
        /// locale ; avant 5 h on est encore sur la veille. Soustraction de
        /// 5 heures sur la ligne du temps, comme `Calendar.date(byAdding: .hour)`.
        public static int Challenge(DateTimeOffset instant, TimeZoneInfo zone) =>
            Of(ToLocal(instant.AddHours(-ResetHour), zone).DateTime);

        public static int Challenge(IClock clock) => Challenge(clock.Now, clock.Zone);

        /// Clé du jour calendaire local, bascule à minuit (quota des pubs récompensées).
        public static int Calendar(DateTimeOffset instant, TimeZoneInfo zone) =>
            Of(ToLocal(instant, zone).DateTime);

        public static int Calendar(IClock clock) => Calendar(clock.Now, clock.Zone);

        /// Date locale (sans heure) de l'instant.
        public static DateTime LocalDate(DateTimeOffset instant, TimeZoneInfo zone) =>
            ToLocal(instant, zone).Date;

        /// Instant du début de la journée locale (`Calendar.startOfDay(for:)`).
        public static DateTimeOffset StartOfDay(DateTimeOffset instant, TimeZoneInfo zone)
        {
            DateTime midnight = LocalDate(instant, zone);
            TimeSpan offset = zone.IsInvalidTime(midnight) ? zone.GetUtcOffset(midnight.AddHours(1)) : zone.GetUtcOffset(midnight);
            return new DateTimeOffset(midnight, offset);
        }
    }
}
