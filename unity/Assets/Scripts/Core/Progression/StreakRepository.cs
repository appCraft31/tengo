#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace TenGo.Core
{
    /// Série de jours consécutifs de jeu et boucliers (StreakManager.swift).
    /// Un jour manqué consomme un bouclier s'il y en a un, sinon la série
    /// repart à 1, sans drame. Un recul d'horloge n'entame jamais la série ni
    /// un bouclier. Jours comptés dans le calendrier local du joueur.
    ///
    /// Le dernier jour joué est stocké comme iOS : début de la journée locale,
    /// en secondes de référence Swift (`timeIntervalSinceReferenceDate`).
    public sealed class StreakRepository
    {
        /// Boucliers détenus au maximum : le bouclier doit rester rare.
        public const int MaxShields = 2;

        /// Paliers (jour → pièces + booster offert éventuel).
        public static readonly IReadOnlyList<StreakMilestone> Milestones = new[]
        {
            new StreakMilestone(3, 25, null, 0),
            new StreakMilestone(7, 0, Booster.Hint, 1),
            new StreakMilestone(14, 30, Booster.Shuffle, 1),
            new StreakMilestone(21, 75, null, 0),
            new StreakMilestone(30, 60, Booster.Hammer, 1),
            new StreakMilestone(50, 150, null, 0),
            new StreakMilestone(100, 300, null, 0),
            new StreakMilestone(365, 1000, null, 0),
        };

        private readonly ISaveStore _store;
        private readonly EconomyRepository _economy;
        private readonly IClock _clock;

        public StreakRepository(ISaveStore store, EconomyRepository economy, IClock clock)
        {
            _store = store;
            _economy = economy;
            _clock = clock;
        }

        public int Current => _store.GetInt(SaveKeys.StreakCurrent);

        public int Best => _store.GetInt(SaveKeys.StreakBest);

        public int ShieldCount => _store.GetInt(SaveKeys.StreakShieldCount);

        /// Premier palier strictement supérieur à la série donnée (null au-delà du dernier).
        public static StreakMilestone? NextMilestone(int current) => Milestones.FirstOrDefault(m => m.Day > current);

        private DateTime? LastPlayedDay()
        {
            double stored = _store.GetDouble(SaveKeys.StreakLastPlayed);
            if (stored == 0) return null;
            return DayKeys.LocalDate(SwiftDate.FromReferenceSeconds(stored), _clock.Zone);
        }

        /// Le joueur a-t-il déjà joué aujourd'hui (calendrier local) ? Une
        /// dernière partie « dans le futur » (horloge reculée) compte comme jouée.
        public bool HasPlayedToday() => HasPlayedToday(_clock.Now);

        public bool HasPlayedToday(DateTimeOffset now)
        {
            DateTime? last = LastPlayedDay();
            if (last == null) return false;
            return (DayKeys.LocalDate(now, _clock.Zone) - last.Value).Days <= 0;
        }

        /// À appeler au lancement d'une partie (pas en reprise). Renvoie vrai si
        /// un bouclier vient d'être consommé pour absorber un jour manqué.
        public bool RegisterPlay() => RegisterPlay(_clock.Now);

        public bool RegisterPlay(DateTimeOffset now)
        {
            DateTimeOffset startOfToday = DayKeys.StartOfDay(now, _clock.Zone);
            DateTime? last = LastPlayedDay();
            if (last == null)
            {
                SetCurrent(1, startOfToday);
                return false;
            }
            int gap = (DayKeys.LocalDate(now, _clock.Zone) - last.Value).Days;
            if (gap < 0) return false;   // horloge reculée : ne rien casser
            if (gap == 0) return false;  // déjà compté aujourd'hui
            if (gap == 1)
            {
                SetCurrent(Current + 1, startOfToday);
                return false;
            }
            if (ConsumeShield())
            {
                SetCurrent(Current + 1, startOfToday);
                return true;
            }
            SetCurrent(1, startOfToday);
            return false;
        }

        /// Crédite les paliers nouvellement franchis (chaque palier n'est payé
        /// qu'une fois dans la vie du joueur). Renvoie les paliers payés.
        public List<StreakMilestone> AwardMilestones(int currentStreak)
        {
            MigrateRewardedMilestoneIfNeeded();
            int already = _store.GetInt(SaveKeys.StreakRewardedMilestone);
            var awarded = new List<StreakMilestone>();
            int highest = already;
            int coins = 0;
            foreach (StreakMilestone m in Milestones)
            {
                if (m.Day <= already || currentStreak < m.Day) continue;
                awarded.Add(m);
                coins += m.Coins;
                if (m.Booster is Booster booster) _economy.GrantBooster(booster, m.BoosterQty);
                highest = Math.Max(highest, m.Day);
            }
            if (coins > 0) _economy.AddCoins(coins);
            if (highest != already) _store.SetInt(SaveKeys.StreakRewardedMilestone, highest);
            return awarded;
        }

        /// L'ancien barème (2.3.0) s'arrêtait à 30 jours sans palier 21 : un
        /// joueur déjà payé à 30 est ramené à 20, une seule fois, pour toucher le 21.
        private void MigrateRewardedMilestoneIfNeeded()
        {
            if (_store.GetInt(SaveKeys.StreakRewardedMilestone) != 30 || _store.GetBool(SaveKeys.StreakMilestoneMigrated)) return;
            _store.SetInt(SaveKeys.StreakRewardedMilestone, 20);
            _store.SetBool(SaveKeys.StreakMilestoneMigrated, true);
        }

        /// Ajoute des boucliers, plafonné à `MaxShields`.
        public void AddShield(int count = 1)
        {
            if (count <= 0) return;
            _store.SetInt(SaveKeys.StreakShieldCount, Math.Min(MaxShields, ShieldCount + count));
        }

        private bool ConsumeShield()
        {
            if (ShieldCount <= 0) return false;
            _store.SetInt(SaveKeys.StreakShieldCount, ShieldCount - 1);
            return true;
        }

        private void SetCurrent(int value, DateTimeOffset day)
        {
            _store.SetInt(SaveKeys.StreakCurrent, value);
            _store.SetInt(SaveKeys.StreakBest, Math.Max(value, Best));
            _store.SetDouble(SaveKeys.StreakLastPlayed, SwiftDate.ToReferenceSeconds(day));
        }
    }
}
