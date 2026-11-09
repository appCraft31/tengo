using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TenGo.Core;

namespace TenGo.Tests
{
    /// Port de GridTest.kt.
    public class GridTests
    {
        [Test]
        public void PairOfFivesIsValidMove()
        {
            GridModel g = TestGrids.Empty();
            g.Cells[0][0] = new BubbleModel(5, 0, 0);
            g.Cells[0][1] = new BubbleModel(5, 0, 1);
            Assert.IsTrue(g.HasValidMove());
        }

        [Test]
        public void GridWithNoSumTenHasNoValidMove()
        {
            GridModel g = TestGrids.Empty();
            g.Cells[0][0] = new BubbleModel(9, 0, 0);
            Assert.IsFalse(g.HasValidMove());
        }

        [Test]
        public void FrozenBubblesAreNotTraversable()
        {
            GridModel g = TestGrids.Empty();
            g.Cells[0][0] = new BubbleModel(5, 0, 0);
            g.Cells[0][1] = new BubbleModel(5, 0, 1, isFrozen: true);
            Assert.IsFalse(g.HasValidMove());
        }

        [Test]
        public void AdjacencyAllowsDiagonals()
        {
            Assert.IsTrue(GridModel.IsAdjacent(new Cell(1, 1), new Cell(0, 0)));
            Assert.IsTrue(GridModel.IsAdjacent(new Cell(1, 1), new Cell(1, 2)));
            Assert.IsFalse(GridModel.IsAdjacent(new Cell(1, 1), new Cell(1, 3)));
            Assert.IsFalse(GridModel.IsAdjacent(new Cell(1, 1), new Cell(1, 1)));
        }

        [Test]
        public void PathSumAddsValues()
        {
            GridModel g = TestGrids.Empty();
            g.Cells[0][0] = new BubbleModel(3, 0, 0);
            g.Cells[0][1] = new BubbleModel(7, 0, 1);
            Assert.AreEqual(10, g.PathSum(new[] { new Cell(0, 0), new Cell(0, 1) }));
        }

        [Test]
        public void GravityCompactsColumnDownward()
        {
            GridModel g = TestGrids.Empty();
            int top = GameRules.Rows - 1;
            g.Cells[top][0] = new BubbleModel(4, top, 0);
            List<GravityMove> moves = g.ApplyGravity();
            Assert.IsNull(g.Cells[top][0]);
            Assert.AreEqual(4, g.Cells[0][0]?.Value);
            Assert.IsTrue(moves.Any(m => m.Col == 0 && m.FromRow == top && m.ToRow == 0));
        }

        [Test]
        public void AnchoredBubblesStayInPlace()
        {
            GridModel g = TestGrids.Empty();
            g.Cells[2][0] = new BubbleModel(4, 2, 0, isAnchored: true);
            g.Cells[0][0] = new BubbleModel(3, 0, 0);
            g.ApplyGravity();
            Assert.AreEqual(4, g.Cells[2][0]?.Value);
            Assert.IsTrue(g.Cells[2][0]?.IsAnchored == true);
        }

        [Test]
        public void ObstaclesSegmentColumn()
        {
            GridModel g = TestGrids.Empty();
            g.Blocked[5][0] = true;
            g.Cells[8][0] = new BubbleModel(3, 8, 0);
            g.ApplyGravity();
            Assert.AreEqual(3, g.Cells[6][0]?.Value);
            Assert.IsNull(g.Cells[4][0]);
        }

        [Test]
        public void ThawOnlyAdjacentFrozen()
        {
            GridModel g = TestGrids.Empty();
            g.Cells[0][0] = new BubbleModel(2, 0, 0);
            g.Cells[0][1] = new BubbleModel(1, 0, 1, isFrozen: true);
            g.Cells[5][5] = new BubbleModel(1, 5, 5, isFrozen: true);
            List<Cell> thawed = g.ThawFrozenBubbles(new[] { new Cell(0, 0) });
            Assert.IsTrue(thawed.Contains(new Cell(0, 1)));
            Assert.IsFalse(thawed.Contains(new Cell(5, 5)));
            Assert.IsFalse(g.Cells[0][1]?.IsFrozen ?? true);
            Assert.IsTrue(g.Cells[5][5]?.IsFrozen ?? false);
        }

        [Test]
        public void ReshuffleKeepsPositionsAndGuaranteesPlayable()
        {
            GridModel g = TestGrids.Empty();
            g.Cells[0][0] = new BubbleModel(1, 0, 0);
            g.Cells[0][1] = new BubbleModel(1, 0, 1);
            g.Cells[1][0] = new BubbleModel(1, 1, 0, isAnchored: true);
            g.ReshuffleValues(new SeededGenerator(99));
            Assert.IsTrue(g.Cells[0][0] != null && g.Cells[0][1] != null && g.Cells[1][0] != null);
            Assert.IsTrue(g.Cells[1][0]?.IsAnchored == true);
            Assert.IsTrue(g.HasValidMove());
        }

        [Test]
        public void CountSumTenGroupsDeduplicates()
        {
            GridModel g = TestGrids.Empty();
            g.Cells[0][0] = new BubbleModel(5, 0, 0);
            g.Cells[0][1] = new BubbleModel(5, 0, 1);
            Assert.AreEqual(1, g.CountSumTenGroups(2));
        }

        [Test]
        public void ShowcasePathFindsLongest()
        {
            GridModel g = TestGrids.Empty();
            g.Cells[0][0] = new BubbleModel(2, 0, 0);
            g.Cells[0][1] = new BubbleModel(3, 0, 1);
            g.Cells[0][2] = new BubbleModel(5, 0, 2);
            List<Cell> path = g.ShowcasePath();
            Assert.AreEqual(3, path?.Count);
            Assert.AreEqual(10, g.PathSum(path));
        }

        [Test]
        public void ShowcasePathNullWhenNoMove()
        {
            GridModel g = TestGrids.Empty();
            g.Cells[0][0] = new BubbleModel(9, 0, 0);
            Assert.IsNull(g.ShowcasePath());
        }

        [Test]
        public void RandomGridIsAlwaysPlayable()
        {
            for (int seed = 0; seed < 50; seed++)
                Assert.IsTrue(GridModel.Generate(new SeededGenerator((ulong)seed)).HasValidMove(), "graine " + seed + " non jouable");
        }

        // MARK: - Ajouts Unity

        [Test]
        public void ReshufflePreservesIdentityFrostAndFlower()
        {
            GridModel g = GridModel.Generate(new SeededGenerator(5));
            BubbleModel before = g.Cells[3][3];
            before.IsFrozen = true;
            before.Bloom = 3;
            g.ReshuffleValues(new SeededGenerator(6));
            BubbleModel after = g.Cells[3][3];
            Assert.AreEqual(before.Id, after.Id);
            Assert.IsTrue(after.IsFrozen);
            Assert.AreEqual(3, after.Bloom);
        }

        [Test]
        public void CopyIsDeep()
        {
            GridModel g = GridModel.Generate(new SeededGenerator(8));
            GridModel copy = g.Copy();
            copy.RemoveBubbles(new[] { new Cell(0, 0) });
            copy.Cells[1][1].IsFrozen = true;
            copy.Blocked[2][2] = true;
            Assert.IsNotNull(g.Cells[0][0]);
            Assert.IsFalse(g.Cells[1][1].IsFrozen);
            Assert.IsFalse(g.Blocked[2][2]);
        }

        [Test]
        public void SumTenGroupsAreAdjacentPathsOfTen()
        {
            GridModel g = GridModel.Generate(new SeededGenerator(11));
            List<List<Cell>> groups = g.SumTenGroups(5);
            Assert.Greater(groups.Count, 0);
            foreach (List<Cell> path in groups)
            {
                Assert.AreEqual(10, g.PathSum(path));
                for (int i = 1; i < path.Count; i++) Assert.IsTrue(GridModel.IsAdjacent(path[i - 1], path[i]));
                Assert.AreEqual(path.Count, path.Distinct().Count());
            }
            Assert.AreEqual(groups.Count, g.CountSumTenGroups(5));
        }
    }
}
