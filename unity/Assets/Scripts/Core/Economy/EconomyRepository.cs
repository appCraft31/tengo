#nullable enable
using System;

namespace TenGo.Core
{
    /// Économie du jeu : pièces (CoinManager.swift), inventaire des boosters
    /// (BoosterManager.swift), complétion du défi du jour et onboarding de la
    /// boutique. Persistance locale, aucun backend.
    public sealed class EconomyRepository
    {
        /// Pièces offertes pour avoir terminé le défi du jour.
        public const int DailyChallengeReward = 30;
        /// Pièces d'une vidéo récompensée regardée depuis le menu.
        public const int RewardedMenuCoins = 10;
        /// Au-delà, on cesse de proposer le guide d'achat.
        public const int ShopGuideMaxSnooze = 3;

        private readonly ISaveStore _store;

        public EconomyRepository(ISaveStore store)
        {
            _store = store;
        }

        /// Prévient l'interface d'un changement de solde.
        public event Action<int>? BalanceChanged;

        // MARK: - Pièces

        public int Balance => _store.GetInt(SaveKeys.CoinsBalance);

        public void AddCoins(int amount)
        {
            if (amount <= 0) return;
            _store.SetInt(SaveKeys.CoinsBalance, Balance + amount);
            BalanceChanged?.Invoke(Balance);
        }

        /// Débite si le solde le permet ; faux sinon (ou montant nul).
        public bool SpendCoins(int amount)
        {
            if (amount <= 0 || Balance < amount) return false;
            _store.SetInt(SaveKeys.CoinsBalance, Balance - amount);
            BalanceChanged?.Invoke(Balance);
            return true;
        }

        /// Fin de partie normale : min(10, 5 + score/120). Renvoie les pièces créditées.
        public int AwardForScore(int score)
        {
            int gained = Scoring.CoinsForScore(score);
            if (gained > 0) AddCoins(gained);
            return gained;
        }

        /// Partie continuée : complète la récompense déjà versée. Renvoie le total.
        public int TopUpForScore(int score, int alreadyGiven)
        {
            int total = Scoring.CoinsForScore(score);
            if (total > alreadyGiven) AddCoins(total - alreadyGiven);
            return Math.Max(total, alreadyGiven);
        }

        /// Récompense du défi du jour (une fois par défi).
        public void AwardDailyChallenge() => AddCoins(DailyChallengeReward);

        // MARK: - Boosters

        public int BoosterCount(Booster b) => _store.GetInt(SaveKeys.BoosterInventory(b.Id()));

        public void GrantBooster(Booster b, int quantity = 1)
        {
            if (quantity <= 0) return;
            _store.SetInt(SaveKeys.BoosterInventory(b.Id()), BoosterCount(b) + quantity);
        }

        /// Achat à l'unité (tutoriel, pilule ×1 de la boutique).
        public bool PurchaseBooster(Booster b, int quantity = 1)
        {
            int qty = Math.Max(1, quantity);
            if (!SpendCoins(b.Price() * qty)) return false;
            GrantBooster(b, qty);
            return true;
        }

        /// Achat d'un lot (5 au prix de 4).
        public bool PurchaseBundle(Booster b)
        {
            if (!SpendCoins(b.BundlePrice())) return false;
            GrantBooster(b, b.BundleQuantity());
            return true;
        }

        /// Consomme une unité ; faux si l'inventaire est vide.
        public bool ConsumeBooster(Booster b)
        {
            int current = BoosterCount(b);
            if (current <= 0) return false;
            _store.SetInt(SaveKeys.BoosterInventory(b.Id()), current - 1);
            return true;
        }

        // MARK: - Onboarding des boosters

        public bool ShopGuideCompleted => _store.GetBool(SaveKeys.HasSeenShopPurchaseGuide);

        public void MarkShopGuideCompleted() => _store.SetBool(SaveKeys.HasSeenShopPurchaseGuide, true);

        public int ShopGuideSnooze => _store.GetInt(SaveKeys.ShopGuideSnooze);

        public void SnoozeShopGuide() => _store.SetInt(SaveKeys.ShopGuideSnooze, ShopGuideSnooze + 1);

        public bool BoosterCoachSeen(Booster b) => _store.GetBool(SaveKeys.BoosterCoachSeen(b.Id()));

        /// Posé à la FERMETURE du coach-mark, jamais à son affichage.
        public void MarkBoosterCoachSeen(Booster b) => _store.SetBool(SaveKeys.BoosterCoachSeen(b.Id()), true);

        // MARK: - Défi du jour

        /// Le défi de cette clé du jour a-t-il déjà été terminé ?
        public bool IsDailyCompleted(int dayKey) => _store.GetInt(SaveKeys.DailyLastCompletedDayKey) == dayKey;

        public void MarkDailyCompleted(int dayKey) => _store.SetInt(SaveKeys.DailyLastCompletedDayKey, dayKey);
    }
}
