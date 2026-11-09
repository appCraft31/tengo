using System;

namespace TenGo.Services
{
    /// Résultat d'un achat (StoreManager.PurchaseOutcome).
    public enum PurchaseOutcome { Success, Pending, Cancelled, Failed }

    /// Achat du mode « sans pub » (StoreKit / Play Billing à l'étape 7).
    public interface IAdFreeStore
    {
        /// Prix affiché dans la devise du joueur, `null` tant qu'il est inconnu.
        string DisplayPrice { get; }
        /// Relance le chargement des produits (prix inconnu).
        void LoadProducts();
        void PurchaseAdFree(string source, Action<PurchaseOutcome, string> onDone);
    }
}
