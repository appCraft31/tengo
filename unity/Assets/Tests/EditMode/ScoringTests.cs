using System.Collections.Generic;
using NUnit.Framework;
using TenGo.Core;

namespace TenGo.Tests
{
    /// Port de ScoringTest.kt (valeurs d'ios/tools/score_test).
    public class ScoringTests
    {
        [Test]
        public void ReferenceTableMatchesIos()
        {
            var expected = new Dictionary<int, int>
            {
                [0] = 0, [1] = 0, [2] = 10, [3] = 30, [4] = 100, [5] = 200, [6] = 350,
                [7] = 550, [8] = 800, [9] = 1100, [10] = 1450,
            };
            foreach (var pair in expected) Assert.AreEqual(pair.Value, Scoring.ForPath(pair.Key), "chaîne " + pair.Key);
        }

        [Test]
        public void StrictlyIncreasingAndSuperlinear()
        {
            for (int n = 2; n < 40; n++) Assert.Greater(Scoring.ForPath(n + 1), Scoring.ForPath(n));
            for (int n = 2; n < 40; n++)
                Assert.GreaterOrEqual((double)Scoring.ForPath(n + 1) / (n + 1), (double)Scoring.ForPath(n) / n);
            for (int n = 4; n <= 20; n++) Assert.Greater(Scoring.ForPath(n), n / 2 * 10);
            Assert.Less(Scoring.ForPath(63), 1_000_000);
        }

        [Test]
        public void DisplayMultiplier()
        {
            Assert.AreEqual(0.5, Scoring.DisplayMultiplier(2), 1e-9);
            Assert.AreEqual(0.0, Scoring.DisplayMultiplier(1), 1e-9);
        }

        [Test]
        public void WinBonus()
        {
            Assert.AreEqual(1000, GameRules.WinBonus);
        }

        [Test]
        public void CoinsForScore()
        {
            Assert.AreEqual(5, Scoring.CoinsForScore(0));
            Assert.AreEqual(6, Scoring.CoinsForScore(120));
            Assert.AreEqual(10, Scoring.CoinsForScore(600));
            Assert.AreEqual(10, Scoring.CoinsForScore(100000));
        }

        // MARK: - Ajouts Unity

        /// Arrondi Swift (demi vers l'extérieur) et non bancaire : 3 bulles =
        /// 30 / 30 = 1,0 ; 4 = 100 / 40 = 2,5 ; 7 = 550 / 70 = 7,857 → 7,9.
        [Test]
        public void DisplayMultiplierRoundsLikeSwift()
        {
            Assert.AreEqual(1.0, Scoring.DisplayMultiplier(3), 1e-9);
            Assert.AreEqual(2.5, Scoring.DisplayMultiplier(4), 1e-9);
            Assert.AreEqual(7.9, Scoring.DisplayMultiplier(7), 1e-9);
            Assert.AreEqual(14.5, Scoring.DisplayMultiplier(10), 1e-9);
        }
    }
}
