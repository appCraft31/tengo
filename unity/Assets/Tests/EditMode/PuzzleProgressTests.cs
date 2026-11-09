using NUnit.Framework;
using TenGo.Core;

namespace TenGo.Tests
{
    /// Port de PuzzleProgressTest.kt, sur le catalogue JSON réel.
    public class PuzzleProgressTests
    {
        [Test]
        public void CatalogInvariants()
        {
            PuzzleCatalog catalog = TestGrids.Catalog;
            Assert.AreEqual(20, catalog.Levels(1).Count);
            foreach (PuzzleLevel level in catalog.Levels(1))
            {
                GridModel grid = level.MakeGrid();
                int total = 0;
                for (int r = 0; r < 9; r++) for (int c = 0; c < 7; c++) total += grid.Cells[r][c]?.Value ?? 0;
                Assert.AreEqual(level.Moves * 10, total, "niveau " + level.Index + " : somme = 10 × coups");
                Assert.Greater(level.ThreeStarScore, level.TwoStarScore);
                Assert.IsTrue(grid.HasValidMove());
            }
            Assert.AreEqual(3, catalog.Levels(1)[0].Stars(50));
            Assert.AreEqual(2, catalog.Levels(1)[0].Stars(30));
            Assert.AreEqual(1, catalog.Levels(1)[0].Stars(0));
        }

        [Test]
        public void ProgressNeverDecreasesAndUnlocksNext()
        {
            var repo = new PuzzleProgressRepository(new MemorySaveStore(), TestGrids.Catalog);
            Assert.IsTrue(repo.IsUnlocked(1, 1));
            Assert.IsFalse(repo.IsUnlocked(1, 2));
            repo.Record(1, 1, 3);
            repo.Record(1, 1, 1);
            Assert.AreEqual(3, repo.Stars(1, 1));
            Assert.IsTrue(repo.IsUnlocked(1, 2));
            Assert.AreEqual(3, repo.TotalStars(1));
            Assert.AreEqual(1, repo.CompletedCount(1));
        }

        // MARK: - Ajouts Unity

        /// Le JSON reprend PuzzleCatalog.swift à l'identique (premier et dernier niveau).
        [Test]
        public void JsonMatchesSwiftCatalog()
        {
            PuzzleCatalog catalog = TestGrids.Catalog;
            CollectionAssert.AreEqual(new[] { 1 }, catalog.Worlds);
            PuzzleLevel first = catalog.Level(1, 1);
            Assert.AreEqual("|73|5|72|393|1|", first.Layout);
            Assert.AreEqual(4, first.Moves);
            PuzzleLevel last = catalog.Level(1, 20);
            Assert.AreEqual("121|26315|77352|178|31271|5|", last.Layout);
            Assert.AreEqual(8, last.Moves);
            Assert.AreEqual(180, last.TwoStarScore);
            Assert.AreEqual(250, last.ThreeStarScore);
            Assert.AreEqual("puzzle.world1", PuzzleCatalog.NameKey(1));
            Assert.AreEqual("tenGO_puzzleStars_1_20", SaveKeys.PuzzleStars(1, 20));
        }
    }
}
