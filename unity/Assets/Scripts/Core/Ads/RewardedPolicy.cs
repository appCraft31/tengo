#nullable enable
using System;

namespace TenGo.Core
{
    /// Quota des vidéos récompensées, partie pure de RewardedAdManager.swift :
    /// 5 vidéos « pièces » par jour depuis le menu (remise à zéro à minuit
    /// local), 10 pièces chacune. Les emplacements en partie (indice, seconde
    /// chance) n'ont pas de quota journalier : ils sont bornés par partie.
    public sealed class RewardedPolicy
    {
        public const int DailyLimit = 5;
        public const int MenuCoins = EconomyRepository.RewardedMenuCoins;

        private readonly ISaveStore _store;
        private readonly IClock _clock;

        public RewardedPolicy(ISaveStore store, IClock clock)
        {
            _store = store;
            _clock = clock;
        }

        private int TodayKey => DayKeys.Calendar(_clock);

        /// Vidéos « pièces » déjà regardées aujourd'hui (0 si le jour a changé).
        public int CountToday =>
            _store.GetInt(SaveKeys.RewardedDayKey) == TodayKey ? _store.GetInt(SaveKeys.RewardedDayCount) : 0;

        public int RemainingToday => Math.Max(0, DailyLimit - CountToday);

        public bool CanWatchToday => RemainingToday > 0;

        /// L'emplacement peut être proposé : une vidéo est prête et, pour les
        /// pièces du menu, le quota du jour n'est pas épuisé.
        public bool CanOffer(RewardedPlacement placement, bool adReady) =>
            adReady && (placement != RewardedPlacement.MenuCoins || CanWatchToday);

        /// Récompense obtenue : compte la vue (pièces du menu seulement).
        public void RecordView(RewardedPlacement placement)
        {
            if (placement != RewardedPlacement.MenuCoins) return;
            int current = CountToday;
            _store.SetInt(SaveKeys.RewardedDayKey, TodayKey);
            _store.SetInt(SaveKeys.RewardedDayCount, current + 1);
        }
    }
}
