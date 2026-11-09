using System.Linq;
using NUnit.Framework;
using TenGo.Core;
using TenGo.Services;

namespace TenGo.Tests
{
    /// Logique d'achat de StoreManager.swift (sans SDK) : crédit, bonus du mod
    /// « sans pub », achats en attente, restauration et analytics.
    public class StoreFlowTests
    {
        MemorySaveStore _store;
        EconomyRepository _economy;
        AdFreeRepository _adFree;
        LogAnalytics _log;
        StoreFlow _flow;
        PurchaseOutcome? _outcome;

        static readonly StoreFlow.ProductInfo Info = new("Sans pub", 3.99, "EUR", "T-9");
        const string NoAds = StoreCatalog.NoAdsProductId;
        const string Tier2 = "com.tengo.coins.tier2";

        [SetUp]
        public void SetUp()
        {
            _store = new MemorySaveStore();
            _economy = new EconomyRepository(_store);
            _adFree = new AdFreeRepository(_store, _economy);
            _log = new LogAnalytics { Echo = false };
            _flow = new StoreFlow(_economy, _adFree, _log);
            _outcome = null;
        }

        void Done(PurchaseOutcome o, string _) => _outcome = o;

        [Test]
        public void CoinPackCreditsAndLogsFunnel()
        {
            int before = _economy.Balance;
            Assert.IsTrue(_flow.Begin(Tier2, "shop", Done));
            Assert.IsFalse(_flow.Begin(Tier2, "shop", Done), "un seul achat à la fois par produit");
            _flow.OnPurchased(Tier2, Info);
            Assert.AreEqual(PurchaseOutcome.Success, _outcome);
            Assert.AreEqual(before + 1200, _economy.Balance);
            CollectionAssert.AreEqual(new[] { "iap_tapped", "purchase", "iap_success" }, _log.Sent.Select(e => e.Name).ToArray());
            Assert.AreEqual("shop", _log.Sent[2]["source"]);
        }

        [Test]
        public void NoAdsPurchaseUnlocksAndPaysBonus()
        {
            int before = _economy.Balance;
            _flow.Begin(NoAds, "game_over", Done);
            _flow.OnPurchased(NoAds, Info);
            Assert.IsTrue(_adFree.IsPurchased);
            Assert.AreEqual(before + StoreCatalog.AdFreeBonusCoins, _economy.Balance);
        }

        [Test]
        public void PendingNoAdsPaysBonusOnApproval()
        {
            int before = _economy.Balance;
            _flow.Begin(NoAds, "shop", Done);
            _flow.OnDeferred(NoAds);
            Assert.AreEqual(PurchaseOutcome.Pending, _outcome);
            Assert.IsFalse(_adFree.IsPurchased);
            Assert.AreEqual(1, _log.Count("iap_pending"));

            // Approbation parentale plus tard : transaction hors de la feuille d'achat.
            _flow.OnPurchased(NoAds, Info);
            Assert.IsTrue(_adFree.IsPurchased);
            Assert.AreEqual(before + StoreCatalog.AdFreeBonusCoins, _economy.Balance, "bonus versé une seule fois");
            Assert.AreEqual("transaction_update", _log.Sent.Last()["source"]);
        }

        [Test]
        public void CancelAndFailureAreDistinguished()
        {
            _flow.Begin(Tier2, "shop", Done);
            _flow.OnFailed(Tier2, cancelled: true, "UserCancelled");
            Assert.AreEqual(PurchaseOutcome.Cancelled, _outcome);
            _flow.Begin(Tier2, "shop", Done);
            _flow.OnFailed(Tier2, cancelled: false, "PaymentDeclined");
            Assert.AreEqual(PurchaseOutcome.Failed, _outcome);
            Assert.AreEqual(1, _log.Count("iap_cancelled"));
            Assert.AreEqual("PaymentDeclined", _log.Sent.Last()["reason"]);
        }

        [Test]
        public void RestoreCreditsSilentlyAndReportsOwnership()
        {
            int before = _economy.Balance;
            _flow.BeginRestore();
            _flow.OnPurchased(NoAds, Info);
            Assert.IsTrue(_flow.EndRestore());
            Assert.AreEqual(before, _economy.Balance, "pas de bonus à la restauration");
            Assert.AreEqual(0, _log.Sent.Count, "restauration sans analytics");
        }

        [Test]
        public void RestoreWithNothingOwnedKeepsStickyCache()
        {
            _flow.BeginRestore();
            Assert.IsFalse(_flow.EndRestore());
            _adFree.SetPurchased(true);
            _flow.BeginRestore();
            Assert.IsTrue(_flow.EndRestore(), "cache collant : jamais dégradé par une restauration vide");
        }

        [Test]
        public void RevokedNoAdsIsRemoved()
        {
            _adFree.SetPurchased(true);
            _flow.Credit(NoAds, revoked: true);
            Assert.IsFalse(_adFree.IsPurchased);
        }
    }
}
