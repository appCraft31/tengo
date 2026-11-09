using NUnit.Framework;
using TenGo.Core;

namespace TenGo.Tests
{
    /// Port de GridValidatorTest.kt.
    public class GridValidatorTests
    {
        [Test]
        public void AssessmentIsDeterministicForSameSeed()
        {
            GridModel grid = GridModel.Generate(new SeededGenerator(42));
            GridAssessment a = GridValidator.Assess(grid, new SeededGenerator(9), 3);
            GridAssessment b = GridValidator.Assess(grid, new SeededGenerator(9), 3);
            Assert.AreEqual(a, b);
            Assert.IsTrue(a.HasValidMove);
            Assert.GreaterOrEqual(a.MedianMoves, a.WorstMoves);
            Assert.That(a.Difficulty, Is.InRange(1, 100));
        }

        [Test]
        public void DifficultyBoundsAndInversion()
        {
            Assert.AreEqual(1, GridValidator.DifficultyScore(27, 17, 620));
            Assert.AreEqual(100, GridValidator.DifficultyScore(12, 10, 230));
            Assert.AreEqual(100, GridValidator.DifficultyScore(0, 0, 0));
            Assert.AreEqual(1, GridValidator.DifficultyScore(99, 99, 9999));
            // 0,45·(7/15) + 0,35·(3/7) + 0,20·(195/390) = 0,46.
            Assert.AreEqual(46, GridValidator.DifficultyScore(20, 14, 425));
        }

        [Test]
        public void PublishableRequiresMovesAndMaterial()
        {
            var ok = new GridAssessment(true, 10, 6, 8, 4, 300, 50);
            Assert.IsTrue(ok.IsPublishable);
            Assert.IsFalse(ok.With(worstMoves: 3).IsPublishable);
            Assert.IsFalse(ok.With(shortGroups: 5).IsPublishable);
            Assert.IsFalse(ok.With(hasValidMove: false).IsPublishable);
        }

        [Test]
        public void SimulationConsumesTheBoard()
        {
            GridModel grid = GridModel.Generate(new SeededGenerator(3));
            GridAssessment a = GridValidator.Assess(grid, new SeededGenerator(1), 1);
            Assert.IsFalse(grid.IsGridEmpty());
            Assert.Greater(a.MedianScore, 0);
        }
    }
}
