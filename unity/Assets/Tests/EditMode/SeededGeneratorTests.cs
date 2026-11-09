using NUnit.Framework;
using TenGo.Core;

namespace TenGo.Tests
{
    /// Port de SeededGeneratorTest.kt.
    public class SeededGeneratorTests
    {
        [Test]
        public void SameSeedProducesSameSequence()
        {
            var a = new SeededGenerator(42);
            var b = new SeededGenerator(42);
            for (int i = 0; i < 100; i++) Assert.AreEqual(a.NextULong(), b.NextULong());
        }

        [Test]
        public void DifferentSeedsDiverge()
        {
            var a = new SeededGenerator(1);
            var b = new SeededGenerator(2);
            bool anyDifferent = false;
            for (int i = 0; i < 10; i++) if (a.NextULong() != b.NextULong()) anyDifferent = true;
            Assert.IsTrue(anyDifferent);
        }

        [Test]
        public void KnownSplitMix64Vector()
        {
            var g = new SeededGenerator(0);
            Assert.AreEqual(0xE220A8397B1DCDAFUL, g.NextULong());
            Assert.AreEqual(0x6E789E6AA1B965F4UL, g.NextULong());
            Assert.AreEqual(0x06C45D188009454FUL, g.NextULong());
        }

        [Test]
        public void NextIntStaysInRange()
        {
            var g = new SeededGenerator(123);
            for (int i = 0; i < 1000; i++)
            {
                int v = g.NextInt(1, 9);
                Assert.That(v, Is.InRange(1, 9), "valeur hors plage : " + v);
            }
        }

        [Test]
        public void NextIntIsDeterministic()
        {
            var a = new SeededGenerator(7);
            var b = new SeededGenerator(7);
            for (int i = 0; i < 50; i++) Assert.AreEqual(a.NextInt(0, 100), b.NextInt(0, 100));
            Assert.AreNotEqual(0, 1);
        }
    }
}
