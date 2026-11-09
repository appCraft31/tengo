using NUnit.Framework;
using TenGo.Core;

namespace TenGo.Tests
{
    /// Analyse des liens entrants (DeepLink.swift) : schéma `tengo://` et liens
    /// universels `https://appcraft31.app/tengo/*` prennent les mêmes branches.
    public class DeepLinkTests
    {
        [TestCase("tengo://daily")]
        [TestCase("tengo://daily/")]
        [TestCase("TENGO://Daily")]
        [TestCase("https://appcraft31.app/tengo/daily")]
        [TestCase("https://appcraft31.app/tengo/daily/2026-11-09")]
        [TestCase("https://appcraft31.app/daily")]
        public void DailyLinks(string url) => Assert.AreEqual(DeepLinkKind.Daily, DeepLink.Parse(url).Kind);

        [TestCase("tengo://duel/abc123", "ABC123")]
        [TestCase("tengo://duel/XYZ789", "XYZ789")]
        [TestCase("https://appcraft31.app/tengo/duel/k7mq2p", "K7MQ2P")]
        [TestCase("https://appcraft31.app/duel/K7MQ2P?ref=share", "K7MQ2P")]
        public void DuelLinksCarryTheUppercasedCode(string url, string code)
        {
            var link = DeepLink.Parse(url);
            Assert.AreEqual(DeepLinkKind.Duel, link.Kind);
            Assert.AreEqual(code, link.DuelCode);
        }

        [Test]
        public void UniversalLinkHostIsNeverTakenForADuelCode()
        {
            // Le code se lit dans le chemin : « appcraft31.app » n'est pas un code.
            Assert.IsFalse(DeepLink.Parse("https://appcraft31.app/tengo/duel").IsRecognized);
            Assert.IsFalse(DeepLink.Parse("https://appcraft31.app/tengo/duel/").IsRecognized);
            Assert.IsFalse(DeepLink.Parse("tengo://duel").IsRecognized);
        }

        [TestCase("tengo://progress")]
        [TestCase("tengo://event")]
        [TestCase("https://appcraft31.app/tengo/event")]
        [TestCase("https://appcraft31.app/tengo/progress")]
        public void ProgressLinks(string url) => Assert.AreEqual(DeepLinkKind.Progress, DeepLink.Parse(url).Kind);

        [TestCase(null)]
        [TestCase("")]
        [TestCase("pas une url")]
        [TestCase("tengo://")]
        [TestCase("tengo://shop")]
        [TestCase("https://appcraft31.app/tengo")]
        [TestCase("https://appcraft31.app/tengo/")]
        [TestCase("https://appcraft31.app/tengo/dailyx")]
        [TestCase("https://example.com/other")]
        public void UnknownLinksAreIgnored(string url) => Assert.IsFalse(DeepLink.Parse(url).IsRecognized);

        [Test]
        public void InboxKeepsLinksUntilConsumed()
        {
            var inbox = new DeepLinkInbox();
            Assert.IsFalse(inbox.HasPending);
            Assert.IsTrue(inbox.Receive("tengo://daily"));
            Assert.IsTrue(inbox.Receive("tengo://duel/abc123"));
            Assert.IsTrue(inbox.Receive("tengo://event"));
            Assert.IsFalse(inbox.Receive("tengo://nope"));
            Assert.IsTrue(inbox.HasPending);
            Assert.IsTrue(inbox.ConsumeDaily());
            Assert.IsFalse(inbox.ConsumeDaily());
            Assert.AreEqual("ABC123", inbox.ConsumeDuelCode());
            Assert.IsNull(inbox.ConsumeDuelCode());
            Assert.IsTrue(inbox.ConsumeProgress());
            Assert.IsFalse(inbox.HasPending);
        }
    }
}
