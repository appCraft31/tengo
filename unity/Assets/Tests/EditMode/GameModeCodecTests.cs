using NUnit.Framework;
using TenGo.Core;

namespace TenGo.Tests
{
    /// Port de GameModeCodecTest.kt.
    public class GameModeCodecTests
    {
        [Test]
        public void RoundTrip()
        {
            GameMode[] modes =
            {
                GameMode.Normal, GameMode.Daily, GameMode.Demo, GameMode.Rush,
                GameMode.Puzzle(1, 17), GameMode.Duel(-1234567890123L, "AB3K9Z"), GameMode.Duel(42L, null),
            };
            foreach (GameMode m in modes) Assert.AreEqual(m, GameMode.Decode(m.Encode()));
            Assert.AreEqual(GameMode.Normal, GameMode.Decode(null));
            Assert.AreEqual(GameMode.Normal, GameMode.Decode("garbage"));
        }
    }
}
