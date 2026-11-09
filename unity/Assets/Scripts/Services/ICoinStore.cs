using System;
using System.Collections.Generic;

namespace TenGo.Services
{
    /// Pack de pièces affiché en boutique (produit consommable du store).
    public sealed class CoinPack
    {
        public string ProductId;
        public int Coins;
        public string DisplayPrice;
    }

    /// Packs de pièces et restauration des achats (StoreManager.swift ;
    /// StoreKit / Play Billing à l'étape 7).
    public interface ICoinStore
    {
        /// Produits chargés (vide tant que le chargement n'a pas abouti).
        IReadOnlyList<CoinPack> Packs { get; }
        bool IsLoading { get; }
        /// Charge les produits ; `done(ok)` au retour.
        void LoadProducts(Action<bool> done);
        void Purchase(CoinPack pack, string source, Action<PurchaseOutcome, string> done);
        /// Restaure le mod « sans pub » ; `done(restored)`.
        void RestorePurchases(Action<bool> done);
    }
}
