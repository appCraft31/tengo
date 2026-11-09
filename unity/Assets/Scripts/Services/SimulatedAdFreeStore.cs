using System;
using TenGo.Core;
using TenGo.Fx;

namespace TenGo.Services
{
    /// Boutique simulée : prix fixe, achat réussi après un court délai réel,
    /// crédité par `AdFreeRepository` (drapeau et 500 pièces, comme iOS).
    public sealed class SimulatedAdFreeStore : IAdFreeStore
    {
        readonly AdFreeRepository _adFree;
        public string Price = "3,99 €";
        public PurchaseOutcome NextOutcome = PurchaseOutcome.Success;

        public SimulatedAdFreeStore(AdFreeRepository adFree) { _adFree = adFree; }

        public string DisplayPrice => Price;

        public void LoadProducts() { }

        public void PurchaseAdFree(string source, Action<PurchaseOutcome, string> onDone)
        {
            var outcome = NextOutcome;
            FxTime.AfterRealtime(0.3f, () =>
            {
                if (outcome == PurchaseOutcome.Success) _adFree.OnPurchaseSucceeded();
                else if (outcome == PurchaseOutcome.Pending) _adFree.OnPurchasePending();
                onDone?.Invoke(outcome, outcome == PurchaseOutcome.Success ? null : Loc.T("shop.purchase_failed"));
            });
        }
    }
}
