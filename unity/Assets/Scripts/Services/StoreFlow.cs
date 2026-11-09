using System;
using System.Collections.Generic;
using TenGo.Core;

namespace TenGo.Services
{
    /// Logique d'achat de StoreManager.swift, sans dépendre du SDK du store
    /// (testable en EditMode) : `UnityIapStore` lui transmet les rappels
    /// d'Unity IAP.
    ///
    /// - `credit(transaction)` : pièces d'un pack, ou mod « sans pub » (bonus
    ///   versé à l'approbation d'un achat resté en attente) ;
    /// - achat lancé dans l'app : `iap_tapped`, puis `purchase` + `iap_success`,
    ///   `iap_cancelled`, `iap_pending` ou `iap_failed` ; « sans pub » réussi :
    ///   +500 pièces ; en attente : bonus dû à l'approbation ;
    /// - transaction reçue hors de la feuille d'achat (approbation parentale,
    ///   autre appareil) : `iap_success` avec `source = transaction_update` ;
    /// - restauration : crédit seul, sans analytics ; le résultat est l'état
    ///   du mod (cache « collant » d'`AdFreeRepository`).
    public sealed class StoreFlow
    {
        /// Infos de la transaction pour l'événement `purchase`.
        public readonly struct ProductInfo
        {
            public readonly string Title;
            public readonly double Price;
            public readonly string Currency;
            public readonly string TransactionId;

            public ProductInfo(string title, double price, string currency, string transactionId)
            {
                Title = title;
                Price = price;
                Currency = currency;
                TransactionId = transactionId;
            }
        }

        sealed class Pending
        {
            public string Source;
            public bool AdFree;
            public Action<PurchaseOutcome, string> Done;
        }

        readonly EconomyRepository _economy;
        readonly AdFreeRepository _adFree;
        readonly IAnalytics _analytics;
        readonly Dictionary<string, Pending> _inFlight = new();
        int _restoring;

        public StoreFlow(EconomyRepository economy, AdFreeRepository adFree, IAnalytics analytics)
        {
            _economy = economy;
            _adFree = adFree;
            _analytics = analytics;
        }

        public bool IsPurchasing(string productId) => _inFlight.ContainsKey(productId);

        /// Message affiché pour une issue (`PurchaseOutcome.message`), `null` = succès.
        public static string Message(PurchaseOutcome outcome) => outcome switch
        {
            PurchaseOutcome.Success => null,
            PurchaseOutcome.Cancelled => Loc.T("shop.purchase_cancelled"),
            PurchaseOutcome.Pending => Loc.T("shop.purchase_pending"),
            _ => Loc.T("shop.purchase_failed"),
        };

        /// Le joueur lance un achat (`purchase(_:source:)`) ; faux s'il y en a déjà un sur ce produit.
        public bool Begin(string productId, string source, Action<PurchaseOutcome, string> done)
        {
            if (_inFlight.ContainsKey(productId)) return false;
            _inFlight[productId] = new Pending { Source = source, AdFree = productId == StoreCatalog.NoAdsProductId, Done = done };
            _analytics?.IapTapped(productId, source);
            return true;
        }

        /// `credit(transaction)`.
        public void Credit(string productId, bool revoked = false)
        {
            if (productId == StoreCatalog.NoAdsProductId)
            {
                _adFree.OnTransaction(revoked);
                return;
            }
            int amount = StoreCatalog.CoinsFor(productId);
            if (amount > 0 && !revoked) _economy.AddCoins(amount);
        }

        /// Transaction vérifiée (Unity IAP `ProcessPurchase`, hors achat différé).
        public void OnPurchased(string productId, ProductInfo info, string environment = null)
        {
            Credit(productId);
            if (_inFlight.TryGetValue(productId, out var p))
            {
                _inFlight.Remove(productId);
                _analytics?.Purchase(productId, info.Title ?? productId, info.Price, info.Currency ?? "", info.TransactionId ?? "");
                _analytics?.IapSucceeded(productId, p.Source, environment);
                if (p.AdFree) _economy.AddCoins(StoreCatalog.AdFreeBonusCoins);
                p.Done?.Invoke(PurchaseOutcome.Success, null);
                return;
            }
            if (_restoring == 0) _analytics?.IapSucceeded(productId, "transaction_update", environment);
        }

        /// Achat en attente de validation (« Demander l'autorisation », SCA).
        public void OnDeferred(string productId)
        {
            if (!_inFlight.TryGetValue(productId, out var p)) return;
            _inFlight.Remove(productId);
            _analytics?.IapPending(productId, p.Source);
            if (p.AdFree) _adFree.OnPurchasePending();
            p.Done?.Invoke(PurchaseOutcome.Pending, Message(PurchaseOutcome.Pending));
        }

        /// Échec ou annulation de la feuille d'achat.
        public void OnFailed(string productId, bool cancelled, string reason)
        {
            if (!_inFlight.TryGetValue(productId, out var p)) return;
            _inFlight.Remove(productId);
            if (cancelled) _analytics?.IapCancelled(productId, p.Source);
            else _analytics?.IapFailed(productId, p.Source, reason ?? "unknown");
            var outcome = cancelled ? PurchaseOutcome.Cancelled : PurchaseOutcome.Failed;
            p.Done?.Invoke(outcome, Message(outcome));
        }

        /// Restauration (`restorePurchases`) : encadre les transactions restaurées.
        public void BeginRestore() => _restoring++;

        /// Fin de restauration : vrai si le mod « sans pub » est possédé.
        public bool EndRestore()
        {
            if (_restoring > 0) _restoring--;
            return _adFree.IsPurchased;
        }
    }
}
