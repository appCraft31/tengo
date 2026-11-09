using System.Linq;
using NUnit.Framework;
using TenGo.Core;

namespace TenGo.Tests
{
    /// Cadeau de bienvenue du tutoriel jouable (4.0) : versé une seule fois,
    /// persisté, et dû à un joueur repris d'une app native qui ne l'a jamais reçu.
    public class WelcomeGiftTests
    {
        [Test]
        public void PaysFiftyCoinsAndTwoHints_OnlyOnce()
        {
            var store = new MemorySaveStore();
            var economy = new EconomyRepository(store);
            var gift = new WelcomeGift(store, economy);
            Assert.IsFalse(gift.IsPaid);
            Assert.IsTrue(gift.PayIfNeeded());
            Assert.AreEqual(50, economy.Balance);
            Assert.AreEqual(2, economy.BoosterCount(Booster.Hint));
            Assert.IsFalse(gift.PayIfNeeded(), "second passage : rien");
            Assert.IsFalse(gift.PayIfNeeded());
            Assert.AreEqual(50, economy.Balance);
            Assert.AreEqual(2, economy.BoosterCount(Booster.Hint));
        }

        [Test]
        public void PaymentIsPersisted_AcrossRelaunches()
        {
            var store = new MemorySaveStore();
            Assert.IsTrue(new WelcomeGift(store, new EconomyRepository(store)).PayIfNeeded());
            // Relance : nouveaux dépôts sur la même sauvegarde (JSON aller-retour).
            var reloaded = SaveStoreJson.FromJson(SaveStoreJson.ToJson(store));
            var economy = new EconomyRepository(reloaded);
            var gift = new WelcomeGift(reloaded, economy);
            Assert.IsTrue(gift.IsPaid);
            Assert.IsFalse(gift.PayIfNeeded());
            Assert.AreEqual(50, economy.Balance);
        }

        [Test]
        public void NativePlayerWhoNeverGotIt_ReceivesItOnce_OnTopOfTheirCoins()
        {
            // Sauvegarde reprise d'une 3.x : tutoriel déjà vu, des pièces, des indices, pas de clé de cadeau.
            var store = new MemorySaveStore();
            store.SetBool(SaveKeys.HasSeenTutorial, true);
            store.SetInt(SaveKeys.CoinsBalance, 340);
            store.SetInt(SaveKeys.BoosterInventory(Booster.Hint.Id()), 3);
            var economy = new EconomyRepository(store);
            var gift = new WelcomeGift(store, economy);
            Assert.IsFalse(gift.IsPaid);
            Assert.IsTrue(gift.PayIfNeeded());
            Assert.AreEqual(390, economy.Balance);
            Assert.AreEqual(5, economy.BoosterCount(Booster.Hint));
            Assert.IsFalse(gift.PayIfNeeded());
        }

        [Test]
        public void GiftKeyIsNotAnIosKey()
        {
            // Clé propre à la 4.0 : aucune règle de reprise native ne doit la poser.
            Assert.IsFalse(AndroidLegacyImporter.Rules.Any(r => r.key == SaveKeys.WelcomeGiftPaid));
        }
    }

    /// Mini niveau du tutoriel jouable : étapes, refus et gravité.
    public class TutorialLevelTests
    {
        static readonly (int, int)[] FourSix = { (1, 0), (1, 1) };

        [Test]
        public void StepOne_OnlyFourAndSixAreActive()
        {
            var level = new TutorialLevel();
            Assert.AreEqual(0, level.Step);
            Assert.AreEqual(3, level.Size);
            Assert.IsTrue(level.IsActive(1, 0));
            Assert.IsTrue(level.IsActive(1, 1));
            Assert.IsFalse(level.IsActive(0, 0));
            Assert.IsFalse(level.Submit(new[] { (0, 1), (0, 0) }), "2 + 5 hors des bulles vives");
            Assert.IsTrue(level.Submit(FourSix));
            Assert.AreEqual(1, level.Step);
            Assert.AreEqual(4, level.Size);
        }

        [Test]
        public void StepTwo_NeedsThreeBubbles_DemoPathWorks()
        {
            var level = new TutorialLevel();
            level.Submit(FourSix);
            // 3 + 7 (0,0)-(0,1) fait 10 à deux bulles : refusé, l'étape demande au moins trois bulles.
            Assert.AreEqual(10, level.Sum(new[] { (0, 0), (0, 1) }));
            Assert.IsFalse(level.Submit(new[] { (0, 0), (0, 1) }));
            Assert.IsFalse(level.Submit(new[] { (1, 0), (3, 0) }), "pas voisines");
            Assert.AreEqual(10, level.Sum(TutorialLevel.Step1Demo));
            Assert.IsTrue(level.Submit(TutorialLevel.Step1Demo));
            Assert.AreEqual(2, level.Step);
        }

        [Test]
        public void StepThree_GravityThenAnyTen()
        {
            var level = new TutorialLevel();
            level.Submit(FourSix);
            level.Submit(TutorialLevel.Step1Demo);
            // Colonne 0 : 3 et 8 restent et tombent en bas ; deux nouvelles bulles arrivent en haut.
            Assert.AreEqual(3, level.Value(2, 0));
            Assert.AreEqual(8, level.Value(3, 0));
            Assert.AreEqual(-2, level.FallFrom[0, 0]);
            Assert.AreEqual(0, level.FallFrom[2, 0]);
            // Colonne 2 intacte.
            Assert.AreEqual(2, level.Value(0, 2));
            Assert.AreEqual(0, level.FallFrom[0, 2]);
            for (int r = 0; r < 4; r++)
                for (int c = 0; c < 4; c++) Assert.That(level.Value(r, c), Is.InRange(1, 9));
            Assert.IsTrue(level.HasTen());
            Assert.IsTrue(level.Submit(new[] { (2, 0), (2, 1) }), $"3 + {level.Value(2, 1)}");
            Assert.IsTrue(level.IsFinished);
            Assert.IsFalse(level.Submit(new[] { (0, 0), (0, 1) }));
        }

        [Test]
        public void PathRules_NoRepeat_Diagonals()
        {
            var level = new TutorialLevel();
            level.Submit(FourSix);
            Assert.IsTrue(level.IsValidPath(new[] { (1, 1), (2, 0) }), "diagonale");
            Assert.IsFalse(level.IsValidPath(new[] { (1, 1), (2, 0), (1, 1) }), "doublon");
            Assert.IsFalse(level.IsValidPath(new[] { (0, 0), (4, 0) }));
        }

        [Test]
        public void SkipGoesToTheEnd()
        {
            var level = new TutorialLevel();
            level.Skip();
            Assert.IsTrue(level.IsFinished);
        }
    }
}
