#nullable enable
using System;

namespace TenGo.Core
{
    /// Calendrier des notifications locales (NotificationManager.swift), en
    /// heure locale, sans dépendre d'un SDK. Trois notifications, mêmes
    /// identifiants que l'app iOS, plus l'ancien rappel de la 2.3.0 à purger.
    public static class NotificationPlan
    {
        public const string DailyAvailableId = "tenGO.dailyAvailable";
        public const string StreakAtRiskId = "tenGO.streakAtRisk";
        public const string SeedBloomId = "tenGO.seedBloom";
        /// Rappel générique répétitif de la 2.3.0 : il survit à la mise à jour,
        /// d'où une annonce explicite à chaque planification du défi du jour.
        public const string LegacyReminderId = "tenGO.dailyReminder";

        /// Identifiants entiers Android (pas de chaîne côté Android).
        public const int AndroidDailyAvailable = 9001;
        public const int AndroidStreakAtRisk = 9002;
        public const int AndroidSeedBloom = 9003;
        /// Canal Android de l'app native (réglages du joueur conservés).
        public const string AndroidChannelId = "tengo_daily";

        /// Parties avant de proposer les notifications.
        public const int PromptThreshold = 3;
        /// « Défi disponible » : le défi est là dès 5 h, on notifie à 9 h.
        public const int DailyAvailableHour = 9;
        /// « Série à risque » : 20 h, marge avant la bascule de 5 h.
        public const int StreakAtRiskHour = 20;
        /// Plage d'annonce de l'éclosion (bornes incluses, `9...21`).
        public const int SeedWindowStart = 9, SeedWindowEnd = 21;

        /// `registerGameAndMaybeRequest` : faut-il demander l'autorisation
        /// après cette partie (compteur déjà incrémenté) ?
        public static bool ShouldRequest(int gamesPlayed, bool alreadyAsked) =>
            gamesPlayed >= PromptThreshold && !alreadyAsked;

        /// Prochaine occurrence du « défi disponible » (strictement après `now`).
        public static DateTime NextDailyAvailable(DateTime nowLocal)
        {
            var today = nowLocal.Date.AddHours(DailyAvailableHour);
            return today > nowLocal ? today : today.AddDays(1);
        }

        /// `rescheduleStreakAtRisk` : ce soir 20 h si la série n'est pas encore
        /// sécurisée et que 20 h n'est pas passé, sinon demain 20 h ; rien sans série.
        public static DateTime? StreakAtRisk(int streak, bool playedToday, DateTime nowLocal)
        {
            if (streak <= 0) return null;
            var tonight = nowLocal.Date.AddHours(StreakAtRiskHour);
            return !playedToday && tonight > nowLocal ? tonight : tonight.AddDays(1);
        }

        /// `scheduleSeedBloom` : une éclosion hors de 9 h – 21 h est annoncée au
        /// réveil (le jour même si 9 h est encore à venir, sinon le lendemain).
        /// Précision à la minute (le déclencheur iOS ignore les secondes).
        public static DateTime SeedBloom(DateTime bloomLocal)
        {
            var fire = new DateTime(bloomLocal.Year, bloomLocal.Month, bloomLocal.Day, bloomLocal.Hour, bloomLocal.Minute, 0, bloomLocal.Kind);
            if (bloomLocal.Hour >= SeedWindowStart && bloomLocal.Hour <= SeedWindowEnd) return fire;
            var morning = bloomLocal.Date.AddHours(SeedWindowStart);
            return morning > bloomLocal ? morning : morning.AddDays(1);
        }
    }
}
