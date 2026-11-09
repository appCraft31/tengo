#if TENGO_IAP
using System;
using System.Collections.Generic;
using System.Linq;
using TenGo.Core;
using UnityEngine;
using UnityEngine.Purchasing;
using UnityEngine.Purchasing.Extension;

namespace TenGo.Services
{
    /// Achats réels (symbole TENGO_IAP) par Unity IAP 4.15 : StoreKit sur iOS,
    /// Play Billing sur Android, mêmes identifiants que les apps natives
    /// (`com.tengo.noads` non consommable, `com.tengo.coins.tier1..4`
    /// consommables). La logique (crédit, bonus, analytics) est dans `StoreFlow`.
    /// La source de vérité du mod « sans pub » reste le store : reçu présent à
    /// l'initialisation → mod actif (cache collant, jamais dégradé ici).
    public sealed class UnityIapStore : ICoinStore, IAdFreeStore, IDetailedStoreListener
    {
        readonly StoreFlow _flow;
        readonly AdFreeRepository _adFree;
        readonly List<CoinPack> _packs = new();
        readonly List<Action<bool>> _loadWaiting = new();
        IStoreController _controller;
        IExtensionProvider _extensions;
        bool _initializing;

        public UnityIapStore(StoreFlow flow, AdFreeRepository adFree)
        {
            _flow = flow;
            _adFree = adFree;
            Initialize();
        }

        public IReadOnlyList<CoinPack> Packs => _packs;
        public bool IsLoading => _initializing;

        public string DisplayPrice => _controller?.products.WithID(StoreCatalog.NoAdsProductId)?.metadata?.localizedPriceString;

        void Initialize()
        {
            if (_initializing || _controller != null) return;
            _initializing = true;
            try
            {
                var builder = ConfigurationBuilder.Instance(StandardPurchasingModule.Instance());
                builder.AddProduct(StoreCatalog.NoAdsProductId, ProductType.NonConsumable);
                foreach (var id in StoreCatalog.CoinPacks.Keys) builder.AddProduct(id, ProductType.Consumable);
                UnityPurchasing.Initialize(this, builder);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Store] IAP indisponible : " + e.Message);
                FinishLoad(false);
            }
        }

        void FinishLoad(bool ok)
        {
            _initializing = false;
            var waiting = _loadWaiting.ToArray();
            _loadWaiting.Clear();
            foreach (var w in waiting) w(ok);
        }

        public void LoadProducts() => LoadProducts(null);

        public void LoadProducts(Action<bool> done)
        {
            if (_controller != null) { done?.Invoke(true); return; }
            if (done != null) _loadWaiting.Add(done);
            Initialize();
        }

        public void Purchase(CoinPack pack, string source, Action<PurchaseOutcome, string> done) => Buy(pack.ProductId, source, done);

        public void PurchaseAdFree(string source, Action<PurchaseOutcome, string> onDone) => Buy(StoreCatalog.NoAdsProductId, source, onDone);

        void Buy(string productId, string source, Action<PurchaseOutcome, string> done)
        {
            var product = _controller?.products.WithID(productId);
            if (product == null || !product.availableToPurchase)
            {
                done?.Invoke(PurchaseOutcome.Failed, StoreFlow.Message(PurchaseOutcome.Failed));
                return;
            }
            if (!_flow.Begin(productId, source, done)) return;
            _controller.InitiatePurchase(product);
        }

        public void RestorePurchases(Action<bool> done)
        {
            if (_extensions == null) { done?.Invoke(_adFree.IsPurchased); return; }
            _flow.BeginRestore();
            void Finish(bool ok, string message) => MainThread.Run(() =>
            {
                if (!ok) Debug.Log("[Store] Échec restauration : " + message);
                done?.Invoke(_flow.EndRestore());
            });
#if UNITY_IOS
            _extensions.GetExtension<IAppleExtensions>().RestoreTransactions(Finish);
#elif UNITY_ANDROID
            _extensions.GetExtension<IGooglePlayStoreExtensions>().RestoreTransactions(Finish);
#else
            Finish(true, null);
#endif
        }

        // MARK: IDetailedStoreListener

        public void OnInitialized(IStoreController controller, IExtensionProvider extensions)
        {
            _controller = controller;
            _extensions = extensions;
#if UNITY_IOS
            extensions.GetExtension<IAppleExtensions>().RegisterPurchaseDeferredListener(p => MainThread.Run(() => _flow.OnDeferred(p.definition.id)));
#endif
            var noAds = controller.products.WithID(StoreCatalog.NoAdsProductId);
            if (noAds != null && noAds.hasReceipt) _adFree.SetPurchased(true);
            _packs.Clear();
            foreach (var p in controller.products.all
                         .Where(p => StoreCatalog.CoinsFor(p.definition.id) > 0 && p.availableToPurchase)
                         .OrderBy(p => StoreCatalog.CoinsFor(p.definition.id)))
                _packs.Add(new CoinPack { ProductId = p.definition.id, Coins = StoreCatalog.CoinsFor(p.definition.id), DisplayPrice = p.metadata.localizedPriceString });
            Debug.Log($"[Store] Produits chargés : {controller.products.all.Count(p => p.availableToPurchase)}/{StoreCatalog.CoinPacks.Count + 1}");
            FinishLoad(true);
        }

        public void OnInitializeFailed(InitializationFailureReason error) => OnInitializeFailed(error, null);

        public void OnInitializeFailed(InitializationFailureReason error, string message)
        {
            Debug.LogWarning("[Store] Échec chargement produits : " + error + " " + message);
            FinishLoad(false);
        }

        public PurchaseProcessingResult ProcessPurchase(PurchaseEventArgs args)
        {
            var product = args.purchasedProduct;
            string id = product.definition.id;
#if UNITY_ANDROID
            if (_extensions != null && _extensions.GetExtension<IGooglePlayStoreExtensions>().IsPurchasedProductDeferred(product))
            {
                _flow.OnDeferred(id);
                return PurchaseProcessingResult.Pending;
            }
#endif
            var meta = product.metadata;
            _flow.OnPurchased(id, new StoreFlow.ProductInfo(meta?.localizedTitle, (double)(meta?.localizedPrice ?? 0m), meta?.isoCurrencyCode, product.transactionID),
                Debug.isDebugBuild ? "debug" : null);
            return PurchaseProcessingResult.Complete;
        }

        public void OnPurchaseFailed(Product product, PurchaseFailureReason reason) =>
            _flow.OnFailed(product.definition.id, reason == PurchaseFailureReason.UserCancelled, reason.ToString());

        public void OnPurchaseFailed(Product product, PurchaseFailureDescription description) =>
            _flow.OnFailed(product.definition.id, description.reason == PurchaseFailureReason.UserCancelled,
                description.reason + (string.IsNullOrEmpty(description.message) ? "" : ": " + description.message));
    }
}
#endif
