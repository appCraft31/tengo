using System;
using System.Collections.Generic;
using TenGo.Core;
using TenGo.Fx;

namespace TenGo.Services
{
    /// Boutique de pièces simulée : les quatre packs de `StoreCatalog` aux prix
    /// de la capture App Store, achat crédité après un court délai réel ;
    /// la restauration rend le mod « sans pub » si `OwnsAdFree`.
    public sealed class SimulatedCoinStore : ICoinStore
    {
        static readonly string[] Prices = { "0,99 €", "1,99 €", "3,99 €", "7,99 €" };

        readonly EconomyRepository _economy;
        readonly AdFreeRepository _adFree;
        readonly List<CoinPack> _packs = new();
        public float Latency = 0.3f;
        public bool FailLoads;
        public PurchaseOutcome NextOutcome = PurchaseOutcome.Success;
        /// Le compte du store possède déjà le mod « sans pub » (restauration).
        public bool OwnsAdFree;

        public SimulatedCoinStore(EconomyRepository economy, AdFreeRepository adFree, bool preloaded = true)
        {
            _economy = economy;
            _adFree = adFree;
            if (preloaded) Fill();
        }

        void Fill()
        {
            _packs.Clear();
            int i = 0;
            foreach (var kv in StoreCatalog.CoinPacks)
                _packs.Add(new CoinPack { ProductId = kv.Key, Coins = kv.Value, DisplayPrice = Prices[Math.Min(i++, Prices.Length - 1)] });
        }

        public IReadOnlyList<CoinPack> Packs => _packs;
        public bool IsLoading { get; private set; }

        public void LoadProducts(Action<bool> done)
        {
            IsLoading = true;
            FxTime.AfterRealtime(Latency, () =>
            {
                IsLoading = false;
                if (!FailLoads) Fill();
                done?.Invoke(!FailLoads);
            });
        }

        public void Purchase(CoinPack pack, string source, Action<PurchaseOutcome, string> done)
        {
            var outcome = NextOutcome;
            FxTime.AfterRealtime(Latency, () =>
            {
                if (outcome == PurchaseOutcome.Success) _economy.AddCoins(pack.Coins);
                done?.Invoke(outcome, outcome == PurchaseOutcome.Success ? null : Loc.T("shop.purchase_failed"));
            });
        }

        public void RestorePurchases(Action<bool> done)
        {
            FxTime.AfterRealtime(Latency, () =>
            {
                bool restored = OwnsAdFree;
                if (restored) _adFree.SetPurchased(true);
                done?.Invoke(restored);
            });
        }
    }
}
