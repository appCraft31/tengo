using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TenGo.Core;

namespace TenGo.Tests
{
    /// Port de MirrorTest.kt (miroir cognitif et graines de dix).
    public class MirrorTests
    {
        [Test]
        public void FortyOneWaysToMakeTen()
        {
            Assert.AreEqual(41, Partition.All.Count);
            Assert.IsTrue(Partition.All.All(p => p.Sum() == 10 && p.Count >= 2 && p.All(v => v >= 1 && v <= 9)));
            Assert.AreEqual(41, Partition.All.Select(p => Partition.Key(p)).Distinct().Count());
            Assert.AreEqual("1+2+7", Partition.Key(new[] { 7, 1, 2 }));
            CollectionAssert.AreEqual(new[] { 1, 9 }, Partition.All[0]);
        }

        [Test]
        public void TallyIgnoresPausesAndCountsFalls()
        {
            var t = new MirrorTally();
            t.Record(new[] { 3, 7 }, 3.0, false, false);
            t.Record(new[] { 1, 2, 7 }, 2.5, true, true);
            t.Record(new[] { 7, 3 }, 40.0, false, true);
            Assert.AreEqual(2, t.LookCount);
            Assert.AreEqual(1, t.FallChains);
            Assert.AreEqual(2, t.FallEligible);
            CollectionAssert.AreEqual(new[] { "3+7", "1+2+7" }, t.Partitions);
        }

        private static MirrorTally Tally(int repeat, int[] values, double look)
        {
            var t = new MirrorTally();
            for (int i = 0; i < repeat; i++) t.Record(values, look, false, true);
            return t;
        }

        [Test]
        public void InsightPriorityMatchesIos()
        {
            MirrorData data = MirrorData.Empty;
            long now = 1_000_000_000L;
            var first = new MirrorTally();
            first.Record(new[] { 3, 7 }, 3.0, false, false);
            first.Record(new[] { 1, 2, 7 }, 2.5, true, true);
            var (d1, i1) = Mirror.FinishGame(data, first, now);
            Assert.AreEqual(MirrorInsight.Discovered("1+2+7", 2), i1);
            data = d1;
            for (int i = 0; i < 3; i++)
            {
                now += 60_000;
                data = Mirror.FinishGame(data, Tally(6, new[] { 3, 7 }, 4.0), now).data;
            }
            now += 60_000;
            var (d2, i2) = Mirror.FinishGame(data, Tally(6, new[] { 7, 3 }, 2.0), now);
            Assert.AreEqual(MirrorInsightKind.Faster, i2?.Kind, "attendu Faster, obtenu " + i2);
            now += 60_000;
            var (_, i3) = Mirror.FinishGame(d2, Tally(6, new[] { 3, 7 }, 4.0), now);
            Assert.AreEqual(MirrorInsight.NextTarget(2, 2, 41), i3);
            Assert.IsNotNull(Mirror.WeeklyReading(d2, MirrorSkill.Look, now).Value);
        }

        [Test]
        public void EmptyGameRecordsNothing()
        {
            var (data, insight) = Mirror.FinishGame(MirrorData.Empty, new MirrorTally(), 0);
            Assert.IsNull(insight);
            Assert.AreEqual(0, data.Records.Count);
        }

        [Test]
        public void SeedLifecycle()
        {
            var (none, tooShort) = SeedRules.Offer(null, 3, 0);
            Assert.IsNull(none);
            Assert.AreEqual(SeedOutcome.TooShort(4), tooShort);

            var (seed, planted) = SeedRules.Offer(null, 4, 0);
            Assert.AreEqual(SeedOutcome.Planted(2, SeedRules.GrowthMs), planted);
            var (stronger, outcome) = SeedRules.Offer(seed, 7, 1000);
            Assert.AreEqual(SeedOutcome.Strengthened(4, SeedRules.GrowthMs), outcome);
            Assert.AreEqual(SeedOutcome.StillGrowing(SeedRules.GrowthMs), SeedRules.Offer(stronger, 5, 2000).outcome);
            Assert.AreEqual(SeedOutcome.FlowerWaiting(4), SeedRules.Offer(stronger, 5, SeedRules.GrowthMs).outcome);
        }

        [Test]
        public void FlowerMultipliesItsChainAndSurvivesShuffle()
        {
            GridModel grid = GridModel.Generate(new SeededGenerator(42));
            Cell at = grid.PlantFlower(3, new SeededGenerator(1)).Value;
            Assert.AreEqual(3, grid.BloomMultiplier(new[] { at }));
            grid.ReshuffleValues(new SeededGenerator(2));
            Assert.AreEqual(3, grid.Cells[at.Row][at.Col].Bloom);
            Assert.AreEqual(1, grid.BloomMultiplier(new[] { new Cell((at.Row + 1) % 9, at.Col) }));
        }

        [Test]
        public void FlowerPrefersSmallValues()
        {
            for (int seed = 0; seed < 50; seed++)
            {
                GridModel grid = GridModel.Generate(new SeededGenerator((ulong)seed + 100));
                Cell at = grid.PlantFlower(2, new SeededGenerator((ulong)seed)).Value;
                Assert.LessOrEqual(grid.Cells[at.Row][at.Col].Value, 4);
            }
        }

        // MARK: - Ajouts Unity

        [Test]
        public void HistoryIsCappedAt120Games()
        {
            MirrorData data = MirrorData.Empty;
            for (int i = 0; i < 130; i++) data = Mirror.FinishGame(data, Tally(4, new[] { 1, 9 }, 3.0), i * 1000L).data;
            Assert.AreEqual(Mirror.MaxRecords, data.Records.Count);
            Assert.AreEqual(10 * 1000L, data.Records[0].DateMs);
        }

        [Test]
        public void WeeklyReadingComparesWithOlderGames()
        {
            const long day = 86_400_000L;
            long now = 100 * day;
            var records = new List<MirrorRecord>
            {
                new MirrorRecord(now - 20 * day, 10, 25, 40, 10, 2, 10),   // ancienne : 4 s, 2,5 bulles
                new MirrorRecord(now - 1 * day, 10, 30, 30, 10, 5, 10),    // récente : 3 s, 3 bulles
            };
            var data = new MirrorData(records, new Dictionary<string, long> { ["1+9"] = now - 30 * day, ["2+8"] = now });
            MirrorReading look = Mirror.WeeklyReading(data, MirrorSkill.Look, now);
            Assert.AreEqual(3.0, look.Value.Value, 1e-9);
            Assert.AreEqual(4.0, look.Previous.Value, 1e-9);
            Assert.AreEqual(0.25, look.Improvement(Mirror.LowerIsBetter(MirrorSkill.Look)).Value, 1e-9);
            MirrorReading repertoire = Mirror.WeeklyReading(data, MirrorSkill.Repertoire, now);
            Assert.AreEqual(2.0, repertoire.Value.Value, 1e-9);
            Assert.AreEqual(1.0, repertoire.Previous.Value, 1e-9);
        }
    }
}
