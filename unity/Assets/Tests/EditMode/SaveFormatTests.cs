using System;
using System.Linq;
using NUnit.Framework;
using TenGo.Core;

namespace TenGo.Tests
{
    /// Formats de sauvegarde repris d'iOS (ajouts Unity, sans équivalent Kotlin) :
    /// la reprise des données natives (étape 3) en dépend.
    public class SaveFormatTests
    {
        [Test]
        public void SavedGameRoundTripKeepsTwistsAndFlowers()
        {
            GridModel grid = GridModel.Generate(new SeededGenerator(4));
            grid.PlaceObstacles(3, new SeededGenerator(5));
            grid.Cells[0][0] = new BubbleModel(7, 0, 0, isAnchored: true, isFrozen: true, bloom: 3);
            string json = new SavedGame(grid, 1234).ToJson();
            SavedGame back = SavedGame.FromJson(json);
            Assert.IsNotNull(back);
            Assert.AreEqual(1234, back.Score);
            for (int r = 0; r < GameRules.Rows; r++)
            {
                for (int c = 0; c < GameRules.Cols; c++)
                {
                    Assert.AreEqual(grid.Blocked[r][c], back.Grid.Blocked[r][c]);
                    Assert.AreEqual(grid.Cells[r][c]?.Value, back.Grid.Cells[r][c]?.Value);
                    Assert.AreEqual(grid.Cells[r][c]?.IsAnchored, back.Grid.Cells[r][c]?.IsAnchored);
                    Assert.AreEqual(grid.Cells[r][c]?.IsFrozen, back.Grid.Cells[r][c]?.IsFrozen);
                    Assert.AreEqual(grid.Cells[r][c]?.Bloom, back.Grid.Cells[r][c]?.Bloom);
                }
            }
        }

        /// Sauvegarde iOS antérieure aux twists : ni `isAnchored`, ni `isFrozen`,
        /// ni `bloom`, ni `blocked`.
        [Test]
        public void LegacyIosSaveDecodes()
        {
            string row(int r) => "[" + string.Join(",", Enumerable.Range(0, 7).Select(c =>
                r == 0 && c == 1 ? "null" : "{\"value\":" + (c + 1) + ",\"row\":" + r + ",\"col\":" + c + "}")) + "]";
            string json = "{\"cells\":[" + string.Join(",", Enumerable.Range(0, 9).Select(row)) + "],\"score\":420}";
            SavedGame save = SavedGame.FromJson(json);
            Assert.IsNotNull(save);
            Assert.AreEqual(420, save.Score);
            Assert.IsNull(save.Grid.Cells[0][1]);
            Assert.AreEqual(3, save.Grid.Cells[4][2].Value);
            Assert.IsFalse(save.Grid.Cells[4][2].IsFrozen);
            Assert.IsFalse(save.Grid.Blocked[0][0]);
            Assert.IsNull(SavedGame.FromJson("pas du json"));
        }

        [Test]
        public void HighScoresKeepTopTenAndReplace()
        {
            var games = new GameStateRepository(new MemorySaveStore());
            for (int i = 1; i <= 12; i++) games.AddScore(i * 100);
            CollectionAssert.AreEqual(Enumerable.Range(3, 10).Select(i => i * 100).Reverse(), games.HighScores);
            games.ReplaceScore(1200, 50);
            Assert.AreEqual(1100, games.BestScore);
            Assert.AreEqual(50, games.HighScores.Last());
            games.Save(GridModel.Generate(new SeededGenerator(1)), 77);
            Assert.IsTrue(games.Exists);
            Assert.AreEqual(77, games.Load().Score);
            games.Clear();
            Assert.IsFalse(games.Exists);
            Assert.AreEqual(0, games.RushBest);
        }

        [Test]
        public void SwiftDateConversions()
        {
            Assert.AreEqual(0, SwiftDate.ToReferenceSeconds(new DateTimeOffset(2001, 1, 1, 0, 0, 0, TimeSpan.Zero)), 1e-9);
            Assert.AreEqual(978_307_200_000L, SwiftDate.ReferenceSecondsToUnixMs(0));
            Assert.AreEqual(1.5, SwiftDate.UnixMsToReferenceSeconds(978_307_201_500L), 1e-9);
            var t = new DateTimeOffset(2026, 11, 9, 10, 30, 0, TimeSpan.Zero);
            Assert.AreEqual(t, SwiftDate.FromReferenceSeconds(SwiftDate.ToReferenceSeconds(t)));
        }

        [Test]
        public void MirrorIsStoredUnderIosKeysWithSwiftDates()
        {
            var store = new MemorySaveStore();
            var clock = new FixedClock(new DateTimeOffset(2026, 11, 9, 12, 0, 0, TimeSpan.Zero));
            var mirror = new MirrorRepository(store, clock);
            var tally = new MirrorTally();
            for (int i = 0; i < 4; i++) tally.Record(new[] { 4, 6 }, 1.5, false, false);
            Assert.AreEqual(MirrorInsightKind.Discovered, mirror.FinishGame(tally)?.Kind);
            StringAssert.Contains("\"moves\":4", store.GetString(SaveKeys.MirrorRecords));
            double date = SwiftDate.ToReferenceSeconds(clock.Now);
            StringAssert.Contains("\"4+6\":" + date.ToString(System.Globalization.CultureInfo.InvariantCulture), store.GetString(SaveKeys.MirrorPartitions));
            MirrorData back = new MirrorRepository(store, clock).Data;
            Assert.AreEqual(1, back.Records.Count);
            Assert.AreEqual(clock.Now.ToUnixTimeMilliseconds(), back.Records[0].DateMs);
            Assert.AreEqual(clock.Now.ToUnixTimeMilliseconds(), back.Discovered["4+6"]);
        }

        [Test]
        public void SeedRepositoryPlantsBloomsAndIsTaken()
        {
            var store = new MemorySaveStore();
            var clock = new FixedClock(new DateTimeOffset(2026, 11, 9, 12, 0, 0, TimeSpan.Zero));
            var seeds = new SeedRepository(store, clock);
            Assert.AreEqual(SeedOutcomeKind.TooShort, seeds.Offer(3).outcome.Kind);
            Assert.IsFalse(store.Has(SaveKeys.SeedGarden));
            Assert.AreEqual(SeedOutcomeKind.Planted, seeds.Offer(5).outcome.Kind);
            StringAssert.Contains("\"chain\":5", store.GetString(SaveKeys.SeedGarden));
            Assert.IsNull(seeds.TakeFlower());
            clock.Advance(TimeSpan.FromHours(4));
            Assert.IsTrue(seeds.IsBloomed);
            Assert.AreEqual(3, seeds.TakeFlower());
            Assert.IsNull(seeds.Current);
            Assert.IsFalse(store.Has(SaveKeys.SeedGarden));
        }

        [Test]
        public void MemoryStoreReadsLikeUserDefaults()
        {
            var store = new MemorySaveStore();
            Assert.AreEqual(0, store.GetInt("absent"));
            Assert.IsFalse(store.GetBool("absent"));
            Assert.IsNull(store.GetString("absent"));
            Assert.IsNull(store.GetStringList("absent"));
            store.SetBool("b", true);
            Assert.AreEqual(1, store.GetInt("b"));
            store.SetString("n", "42");
            Assert.AreEqual(42, store.GetInt("n"));
            store.SetInt("i", 3);
            Assert.AreEqual(3.0, store.GetDouble("i"));
            store.Remove("i");
            Assert.IsFalse(store.Has("i"));
        }

        [Test]
        public void DuelHistoryKeepsTwentyMostRecent()
        {
            var history = new DuelHistoryRepository(new MemorySaveStore());
            for (int i = 0; i < 25; i++) history.Remember("C" + i);
            history.Remember("C10");
            Assert.AreEqual(20, history.Codes.Count);
            Assert.AreEqual("C10", history.Codes[0]);
            Assert.AreEqual("C24", history.Codes[1]);
            Assert.AreEqual(1, history.Codes.Count(c => c == "C10"));
        }
    }
}
