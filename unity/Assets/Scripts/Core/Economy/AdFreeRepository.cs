#nullable enable
namespace TenGo.Core
{
    /// Mod « sans pub » (AdFreeManager.swift + bonus de StoreManager.swift).
    /// Coupe bannière et interstitielles ; la vidéo récompensée volontaire reste.
    ///
    /// La source de vérité est le store ; ce cache coupe les pubs dès le
    /// lancement. Il est « collant » : une liste d'achats vide ne le remet pas
    /// à faux (hors ligne, compte déconnecté) ; seule une révocation explicite
    /// (remboursement) désactive le mod.
    public sealed class AdFreeRepository
    {
        private readonly ISaveStore _store;
        private readonly EconomyRepository _economy;

        public AdFreeRepository(ISaveStore store, EconomyRepository economy)
        {
            _store = store;
            _economy = economy;
        }

        public bool IsPurchased => _store.GetBool(SaveKeys.NoAdsPurchased);

        /// Applique l'état d'une transaction (achat, restauration, remboursement).
        /// Renvoie vrai si l'état a changé.
        public bool SetPurchased(bool owned)
        {
            if (owned == IsPurchased) return false;
            _store.SetBool(SaveKeys.NoAdsPurchased, owned);
            return true;
        }

        /// Achat abouti dans l'app : le bonus de pièces est versé tout de suite.
        public void OnPurchaseSucceeded()
        {
            SetPurchased(true);
            _economy.AddCoins(StoreCatalog.AdFreeBonusCoins);
        }

        /// Achat en attente de validation (contrôle parental) : bonus dû à l'approbation.
        public void OnPurchasePending() => _store.SetBool(SaveKeys.NoAdsBonusPending, true);

        /// Transaction reçue (achat approuvé, restauration, autre appareil,
        /// remboursement). Verse le bonus promis d'un achat resté en attente.
        public void OnTransaction(bool revoked)
        {
            SetPurchased(!revoked);
            if (!revoked && _store.GetBool(SaveKeys.NoAdsBonusPending))
            {
                _store.SetBool(SaveKeys.NoAdsBonusPending, false);
                _economy.AddCoins(StoreCatalog.AdFreeBonusCoins);
            }
        }
    }
}
