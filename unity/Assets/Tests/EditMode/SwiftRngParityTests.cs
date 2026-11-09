using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using NUnit.Framework;
using TenGo.Core;

namespace TenGo.Tests
{
    /// Parité au bit près avec la bibliothèque standard Swift (port de
    /// SwiftRngParityTest.kt). Vecteurs produits par le code iOS réel
    /// (`ios/tools/rng_vectors`, TZ=Europe/Paris).
    public class SwiftRngParityTests
    {
        [Test]
        public void RawSplitMix64Seed42()
        {
            var g = new SeededGenerator(42);
            ulong[] expected =
            {
                13679457532755275413UL, 2949826092126892291UL, 5139283748462763858UL, 6349198060258255764UL,
                701532786141963250UL, 16015981125662989062UL, 4028864712777624925UL, 14769051326987775908UL,
            };
            CollectionAssert.AreEqual(expected, Enumerable.Range(0, 8).Select(_ => g.NextULong()).ToArray());
        }

        [Test]
        public void ClosedRange1To9MatchesSwift()
        {
            var g = new SeededGenerator(42);
            int[] expected = { 7, 2, 3, 4, 1, 8, 2, 8, 4, 6, 2, 5, 5, 5, 6, 2 };
            CollectionAssert.AreEqual(expected, Enumerable.Range(0, 16).Select(_ => g.NextInt(1, 9)).ToArray());
        }

        [Test]
        public void HalfOpenRange0To63MatchesSwift()
        {
            var g = new SeededGenerator(42);
            int[] expected = { 46, 10, 17, 21, 2, 54, 13, 50, 21, 38, 12, 31, 32, 32, 41, 12 };
            CollectionAssert.AreEqual(expected, Enumerable.Range(0, 16).Select(_ => g.NextIntBelow(63)).ToArray());
        }

        [Test]
        public void ShuffleMatchesSwiftFisherYates()
        {
            var g = new SeededGenerator(42);
            CollectionAssert.AreEqual(new[] { 8, 2, 4, 6, 1, 11, 7, 5, 9, 10, 0, 3 },
                Enumerable.Range(0, 12).ToList().ShuffledSwift(g));
        }

        [Test]
        public void RandomElementMatchesSwift()
        {
            var g = new SeededGenerator(42);
            IReadOnlyList<int> pool = Enumerable.Range(0, 7).ToList();
            CollectionAssert.AreEqual(new[] { 5, 1, 1, 2, 0, 6, 1, 5 },
                Enumerable.Range(0, 8).Select(_ => pool.RandomElementSwift(g)).ToArray());
        }

        [Test]
        public void SeededGridMatchesSwift()
        {
            GridModel grid = GridModel.Generate(new SeededGenerator(42));
            int[] expected =
            {
                7, 2, 3, 4, 1, 8, 2, 8, 4, 6, 2, 5, 5, 5, 6, 2, 1, 5, 1, 7, 9, 1, 6, 6, 1, 3, 7, 8, 9, 7, 8, 8,
                6, 8, 6, 4, 1, 3, 7, 1, 5, 2, 3, 7, 7, 3, 1, 2, 5, 9, 4, 2, 2, 3, 1, 8, 7, 6, 8, 1, 3, 9, 6,
            };
            int[] actual = Enumerable.Range(0, GameRules.Rows * GameRules.Cols)
                .Select(i => grid.Cells[i / GameRules.Cols][i % GameRules.Cols]?.Value ?? 0).ToArray();
            CollectionAssert.AreEqual(expected, actual);
        }

        [Test]
        public void FullMultiplyMatchesBigInteger()
        {
            var g = new SeededGenerator(7);
            for (int i = 0; i < 2000; i++)
            {
                ulong a = g.NextULong(), b = g.NextULong();
                ulong high = Mul128.Multiply(a, b, out ulong low);
                BigInteger expected = new BigInteger(a) * new BigInteger(b);
                BigInteger actual = (new BigInteger(high) << 64) + new BigInteger(low);
                Assert.AreEqual(expected, actual);
            }
        }
    }
}
