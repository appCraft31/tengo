using System.Linq;
using NUnit.Framework;
using TenGo.Core;

namespace TenGo.Tests
{
    /// Économie (pièces, boosters, thèmes, cosmétiques, mod sans pub) : ajouts
    /// Unity, sans équivalent Kotlin testé.
    public class EconomyTests
    {
        private MemorySaveStore _store;
        private EconomyRepository _economy;

        [SetUp]
        public void SetUp()
        {
            _store = new MemorySaveStore();
            _economy = new EconomyRepository(_store);
        }

        [Test]
        public void CoinsCannotGoNegative()
        {
            _economy.AddCoins(100);
            _economy.AddCoins(-5);
            Assert.IsFalse(_economy.SpendCoins(101));
            Assert.IsFalse(_economy.SpendCoins(0));
            Assert.IsTrue(_economy.SpendCoins(60));
            Assert.AreEqual(40, _economy.Balance);
            Assert.AreEqual(40, _store.GetInt("tenGO_coins"));
        }

        [Test]
        public void ScoreRewardAndContinuationTopUp()
        {
            Assert.AreEqual(7, _economy.AwardForScore(240));
            Assert.AreEqual(10, _economy.TopUpForScore(5000, 7));
            Assert.AreEqual(10, _economy.Balance);
            Assert.AreEqual(10, _economy.TopUpForScore(0, 10));
            Assert.AreEqual(10, _economy.Balance);
        }

        [Test]
        public void BoostersPricesAndInventory()
        {
            Assert.AreEqual(new[] { 30, 50, 80 }, Boosters.All.Select(b => b.Price()).ToArray());
            Assert.AreEqual(200, Booster.Shuffle.BundlePrice());
            Assert.AreEqual("tenGO_booster_hammer", SaveKeys.BoosterInventory(Booster.Hammer.Id()));
            _economy.AddCoins(250);
            Assert.IsTrue(_economy.PurchaseBundle(Booster.Shuffle));
            Assert.AreEqual(5, _economy.BoosterCount(Booster.Shuffle));
            Assert.IsTrue(_economy.PurchaseBooster(Booster.Hint));
            Assert.IsFalse(_economy.PurchaseBooster(Booster.Hammer));
            Assert.AreEqual(20, _economy.Balance);
            Assert.IsTrue(_economy.ConsumeBooster(Booster.Hint));
            Assert.IsFalse(_economy.ConsumeBooster(Booster.Hint));
            Assert.AreEqual("booster.hint_coach", Booster.Hint.CoachKey());
        }

        [Test]
        public void ThemesCatalogAndPurchase()
        {
            Assert.AreEqual(7, ThemeCatalog.All.Count);
            Assert.AreEqual(new[] { "default", "forest", "ocean", "desert", "candy", "space", "night" }, ThemeCatalog.All.Select(t => t.Id).ToArray());
            Assert.IsTrue(ThemeCatalog.All.All(t => t.Bubbles.Count == 9));
            Theme space = ThemeCatalog.ById("space");
            Assert.AreEqual(300, space.Price);
            Assert.AreEqual(0x17 / 255.0, space.Background.R, 1e-9);   // #17142E (palette 4.0)
            Assert.AreEqual(1.0, space.Background.ReadableInk().R, 1e-9);
            var themes = new ThemeRepository(_store, _economy);
            Assert.IsTrue(themes.Owns("default"));
            themes.SetActive("space");
            Assert.AreEqual("default", themes.Active.Id);
            Assert.IsFalse(themes.Purchase(space));
            _economy.AddCoins(300);
            Assert.IsTrue(themes.Purchase(space));
            Assert.AreEqual(0, _economy.Balance);
            themes.SetActive("space");
            Assert.AreEqual("space", themes.Active.Id);
            Assert.IsTrue(themes.Purchase(space));
            Assert.AreEqual(0, _economy.Balance);
        }

        [Test]
        public void CosmeticsAreBoughtPerCategory()
        {
            var cosmetics = new CosmeticRepository(_store, _economy);
            Assert.AreEqual("classic", cosmetics.ActiveBubbleStyle.Id);
            CosmeticStyle neonTrail = CosmeticCatalog.ById(CosmeticKind.Trail, "neon");
            Assert.AreEqual(150, neonTrail.Price);
            Assert.AreEqual("trail.neon", neonTrail.NameKey);
            _economy.AddCoins(150);
            Assert.IsTrue(cosmetics.Purchase(neonTrail));
            Assert.IsTrue(cosmetics.Owns(CosmeticKind.Trail, "neon"));
            Assert.IsFalse(cosmetics.Owns(CosmeticKind.BubbleStyle, "neon"));
            cosmetics.SetActive(CosmeticKind.Trail, "neon");
            cosmetics.SetActive(CosmeticKind.BubbleStyle, "neon");
            Assert.AreEqual("neon", cosmetics.ActiveTrail.Id);
            Assert.AreEqual("classic", cosmetics.ActiveBubbleStyle.Id);
        }

        [Test]
        public void AdFreeBonusIsPaidOnceEvenWhenPending()
        {
            var adFree = new AdFreeRepository(_store, _economy);
            adFree.OnPurchasePending();
            Assert.IsFalse(adFree.IsPurchased);
            adFree.OnTransaction(revoked: false);
            Assert.IsTrue(adFree.IsPurchased);
            Assert.AreEqual(500, _economy.Balance);
            adFree.OnTransaction(revoked: false);
            Assert.AreEqual(500, _economy.Balance);
            adFree.OnTransaction(revoked: true);
            Assert.IsFalse(adFree.IsPurchased);
            Assert.AreEqual(1200, StoreCatalog.CoinsFor("com.tengo.coins.tier2"));
            Assert.AreEqual(0, StoreCatalog.CoinsFor(StoreCatalog.NoAdsProductId));
        }
    }
}
