using NUnit.Framework;
using TenGo.Core;

namespace TenGo.Tests
{
    /// Test témoin de l'étape 1 : l'assembly Core se compile et se teste.
    public class ScreenRefTests
    {
        [Test]
        public void ReferenceScreenIsPortrait()
        {
            Assert.That(ScreenRef.Width, Is.EqualTo(750));
            Assert.That(ScreenRef.Height, Is.EqualTo(1334));
            Assert.That(ScreenRef.Aspect, Is.GreaterThan(1.0));
        }
    }
}
