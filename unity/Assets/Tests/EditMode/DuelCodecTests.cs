using System;
using System.Collections.Generic;
using NUnit.Framework;
using TenGo.Services;

namespace TenGo.Tests
{
    /// Format Firestore des duels, au champ près celui des apps natives 3.3.0
    /// (DuelService.swift, FirestoreDuelService.kt), et refus côté client.
    public class DuelCodecTests
    {
        static readonly DateTimeOffset Now = new(2026, 11, 9, 12, 0, 0, TimeSpan.Zero);

        [Test]
        public void CreateWritesTheNativeFields()
        {
            var fields = DuelCodec.CreateFields(-42L, "uidA", "Alice", 1234, Now + Duel.Lifetime);
            CollectionAssert.AreEquivalent(
                new[] { "seed", "challengerUid", "challengerName", "challengerScore", "opponentUid", "expiresAt", "createdAt" }, fields.Keys);
            Assert.AreEqual(-42L, fields["seed"]);
            Assert.AreEqual(1234L, fields["challengerScore"], "entier 64 bits comme Int Swift");
            Assert.IsTrue(fields.ContainsKey("opponentUid") && fields["opponentUid"] == null, "null exigé par les règles à la création");
            Assert.AreEqual(Now.AddDays(7), fields["expiresAt"]);
            Assert.AreSame(DuelCodec.ServerTimestamp, fields["createdAt"]);
        }

        [Test]
        public void OpponentWritesOnlyItsThreeFields()
        {
            var fields = DuelCodec.OpponentFields("uidB", "Bob", 900);
            CollectionAssert.AreEquivalent(new[] { "opponentUid", "opponentName", "opponentScore" }, fields.Keys);
            Assert.AreEqual(900L, fields["opponentScore"]);
        }

        [Test]
        public void SeedKeepsTheUInt64Bits()
        {
            ulong raw = 0xF00DCAFEDEADBEEFUL;
            var fields = DuelCodec.CreateFields(unchecked((long)raw), "u", "n", 1, Now);
            var duel = DuelCodec.Decode("ABCDEF", fields.WithoutServerTimestamp());
            Assert.AreEqual(raw, unchecked((ulong)duel.Seed));
        }

        [Test]
        public void DecodeRoundTripsAnAnsweredDuel()
        {
            var data = DuelCodec.CreateFields(7, "uidA", "Alice", 1500, Now.AddDays(3)).WithoutServerTimestamp();
            foreach (var kv in DuelCodec.OpponentFields("uidB", "Bob", 1600)) data[kv.Key] = kv.Value;
            var duel = DuelCodec.Decode("K7MQ2P", data);
            Assert.AreEqual("K7MQ2P", duel.Code);
            Assert.AreEqual(1600, duel.OpponentScore);
            Assert.AreEqual(DuelOutcome.Win, duel.OutcomeFor("uidB"));
            Assert.AreEqual(DuelOutcome.Loss, duel.OutcomeFor("uidA"));
            Assert.AreEqual("Alice", duel.RivalName("uidB"));
            Assert.IsNull(duel.OutcomeFor("stranger"));
        }

        [Test]
        public void DecodeAcceptsIntsAndRejectsMissingFields()
        {
            var data = new Dictionary<string, object>
            {
                ["seed"] = 5, ["challengerUid"] = "u", ["challengerScore"] = 10, ["expiresAt"] = Now,
            };
            var duel = DuelCodec.Decode("AAAAAA", data);
            Assert.IsNotNull(duel);
            Assert.AreEqual("", duel.ChallengerName);
            Assert.IsNull(duel.OpponentScore);
            foreach (var key in new[] { "seed", "challengerUid", "challengerScore", "expiresAt" })
            {
                var partial = new Dictionary<string, object>(data);
                partial.Remove(key);
                Assert.IsNull(DuelCodec.Decode("AAAAAA", partial), "sans " + key);
            }
            Assert.IsNull(DuelCodec.Decode("AAAAAA", new Dictionary<string, object>(data) { ["seed"] = "5" }), "mauvais type");
        }

        [Test]
        public void RefusalsComeInTheNativeOrder()
        {
            var duel = new Duel { Code = "AAAAAA", ChallengerUid = "me", ChallengerScore = 10, ExpiresAt = Now.AddMinutes(-1), OpponentScore = 5 };
            Assert.AreEqual("duel.error_expired", DuelRules.JoinRefusal(duel, "me", Now));
            duel.ExpiresAt = Now.AddDays(1);
            Assert.AreEqual("duel.error_already_played", DuelRules.JoinRefusal(duel, "me", Now));
            duel.OpponentScore = null;
            Assert.AreEqual("duel.error_own_duel", DuelRules.JoinRefusal(duel, "me", Now));
            Assert.IsNull(DuelRules.JoinRefusal(duel, "other", Now));
        }

        [Test]
        public void CodesAndNames()
        {
            var rng = new Random(3);
            for (int i = 0; i < 50; i++)
            {
                string code = DuelRules.NewCode(rng);
                Assert.AreEqual(6, code.Length);
                foreach (char c in code) StringAssert.Contains(c.ToString(), DuelRules.Alphabet);
            }
            Assert.AreEqual("K7MQ2P", DuelRules.Normalize("  k7mq2p\n"));
            Assert.AreEqual(40, DuelRules.DisplayName(new string('a', 60)).Length);
            Assert.AreEqual("Zoé", DuelRules.DisplayName("  Zoé "));
        }
    }

    static class DuelFieldsExtensions
    {
        /// Ce que Firestore relit : l'horodatage serveur est devenu une date.
        public static Dictionary<string, object> WithoutServerTimestamp(this Dictionary<string, object> fields)
        {
            var copy = new Dictionary<string, object>(fields);
            copy["createdAt"] = DateTimeOffset.UtcNow;
            return copy;
        }
    }
}
