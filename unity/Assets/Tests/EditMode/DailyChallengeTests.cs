using System;
using NUnit.Framework;
using TenGo.Core;

namespace TenGo.Tests
{
    /// Port de DailyChallengeTest.kt, dont le vecteur de parité iOS.
    public class DailyChallengeTests
    {
        private static DateTimeOffset At(string iso) => DateTimeOffset.Parse(iso, System.Globalization.CultureInfo.InvariantCulture);

        private static TimeZoneInfo Paris()
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById("Europe/Paris"); }
            catch (TimeZoneNotFoundException) { return TimeZoneInfo.FindSystemTimeZoneById("Romance Standard Time"); }
        }

        [Test]
        public void DayKeyFormatYyyymmdd()
        {
            Assert.AreEqual(20260628, DayKeys.Challenge(At("2026-06-28T12:00:00Z"), TimeZoneInfo.Utc));
        }

        [Test]
        public void DayKeyBeforeFiveAmIsPreviousDay()
        {
            Assert.AreEqual(20260627, DayKeys.Challenge(At("2026-06-28T04:59:00Z"), TimeZoneInfo.Utc));
            Assert.AreEqual(20260628, DayKeys.Challenge(At("2026-06-28T05:00:00Z"), TimeZoneInfo.Utc));
        }

        [Test]
        public void DayKeyRespectsTimeZone()
        {
            Assert.AreEqual(20260627, DayKeys.Challenge(At("2026-06-28T03:00:00Z"), TimeZoneInfo.Utc));
            // Ajout Unity : 03:00 UTC = 05:00 à Paris (heure d'été) → déjà le nouveau défi.
            Assert.AreEqual(20260628, DayKeys.Challenge(At("2026-06-28T03:00:00Z"), Paris()));
        }

        [Test]
        public void TwistRotation()
        {
            Assert.AreEqual(DailyTwist.Anchored, DailyTwists.ForDayKey(20260627));
            Assert.AreEqual(DailyTwist.Frozen, DailyTwists.ForDayKey(20260628));
            Assert.AreEqual(DailyTwist.Obstacles, DailyTwists.ForDayKey(20260629));
        }

        [Test]
        public void TwistCounts()
        {
            Assert.AreEqual(9, DailyTwist.Obstacles.Count());
            Assert.AreEqual(6, DailyTwist.Anchored.Count());
            Assert.AreEqual(6, DailyTwist.Frozen.Count());
        }

        [Test]
        public void SameDayProducesIdenticalGrid()
        {
            DailyToday a = DailyChallenge.Make(At("2026-06-28T12:00:00Z"), TimeZoneInfo.Utc);
            DailyToday b = DailyChallenge.Make(At("2026-06-28T12:00:00Z"), TimeZoneInfo.Utc);
            Assert.AreEqual(a.DayKey, b.DayKey);
            Assert.AreEqual(a.Twist, b.Twist);
            for (int r = 0; r < GameRules.Rows; r++)
            {
                for (int c = 0; c < GameRules.Cols; c++)
                {
                    Assert.AreEqual(a.Grid.Cells[r][c]?.Value, b.Grid.Cells[r][c]?.Value, "valeur diff en (" + r + "," + c + ")");
                    Assert.AreEqual(a.Grid.Cells[r][c]?.IsFrozen, b.Grid.Cells[r][c]?.IsFrozen, "gel diff en (" + r + "," + c + ")");
                    Assert.AreEqual(a.Grid.Cells[r][c]?.IsAnchored, b.Grid.Cells[r][c]?.IsAnchored, "ancrage diff en (" + r + "," + c + ")");
                    Assert.AreEqual(a.Grid.Blocked[r][c], b.Grid.Blocked[r][c], "obstacle diff en (" + r + "," + c + ")");
                }
            }
        }

        [Test]
        public void DailyGridIsSolvable()
        {
            Assert.IsTrue(DailyChallenge.Make(At("2026-06-28T12:00:00Z"), TimeZoneInfo.Utc).Grid.IsSolvable());
        }

        [Test]
        public void DifferentDaysProduceDifferentGrids()
        {
            DailyToday d1 = DailyChallenge.Make(At("2026-06-28T12:00:00Z"), TimeZoneInfo.Utc);
            DailyToday d2 = DailyChallenge.Make(At("2026-06-29T12:00:00Z"), TimeZoneInfo.Utc);
            bool different = false;
            for (int r = 0; r < GameRules.Rows; r++)
                for (int c = 0; c < GameRules.Cols; c++)
                    if (d1.Grid.Cells[r][c]?.Value != d2.Grid.Cells[r][c]?.Value) different = true;
            Assert.IsTrue(different);
        }

        [Test]
        public void TargetCycle()
        {
            Assert.AreEqual(DailyTarget.Easy, DailyTargets.ForDay(0));
            Assert.AreEqual(DailyTarget.Hard, DailyTargets.ForDay(2));
            Assert.AreEqual(DailyTarget.Medium, DailyTargets.ForDay(6));
        }

        [Test]
        public void DailyGridMatchesIos()
        {
            // Vecteur produit par ios/tools/rng_vectors (TZ=Europe/Paris, 10/09/2026 12:00).
            DailyToday today = DailyChallenge.Make(At("2026-09-10T10:00:00Z"), Paris());
            Assert.AreEqual(20260910, today.DayKey);
            Assert.AreEqual(DailyTwist.Frozen, today.Twist);
            Assert.AreEqual(71, today.Difficulty);
            string[] expected = ("6,6,2,2,1,2,1,9f,5f,1,1,7,1,7,3,3,4,6,1,2,9,6,8,1,4,7,7,4,7,5,7,1,6,1,5,1,7,9f,6,6," +
                "1f,9,2,1f,6,8,3,6,5,5,9,9,9,5,6,9,4,3,8,7,8,1f,9").Split(',');
            for (int i = 0; i < expected.Length; i++)
            {
                int r = i / GameRules.Cols, c = i % GameRules.Cols;
                string token = expected[i];
                BubbleModel cell = today.Grid.Cells[r][c];
                if (token == "X") Assert.IsTrue(today.Grid.Blocked[r][c], "obstacle attendu en (" + r + "," + c + ")");
                else if (token == ".") Assert.IsNull(cell, "vide attendu en (" + r + "," + c + ")");
                else
                {
                    Assert.AreEqual(token[0] - '0', cell?.Value, "valeur en (" + r + "," + c + ")");
                    Assert.AreEqual(token.Contains("a"), cell?.IsAnchored, "ancrage en (" + r + "," + c + ")");
                    Assert.AreEqual(token.Contains("f"), cell?.IsFrozen, "gel en (" + r + "," + c + ")");
                }
            }
        }

        [Test]
        public void PuzzleLayoutDecodesBottomUp()
        {
            GridModel grid = GridModel.FromPuzzleLayout("|73|5|72|393|1|");
            Assert.IsNull(grid.Cells[0][0]);
            Assert.AreEqual(7, grid.Cells[0][1]?.Value);
            Assert.AreEqual(3, grid.Cells[1][1]?.Value);
            Assert.AreEqual(5, grid.Cells[0][2]?.Value);
            Assert.AreEqual(3, grid.Cells[2][4]?.Value);
            Assert.AreEqual(1, grid.Cells[0][5]?.Value);
            Assert.IsNull(grid.Cells[0][6]);
            int total = 0;
            for (int r = 0; r < GameRules.Rows; r++)
                for (int c = 0; c < GameRules.Cols; c++)
                    total += grid.Cells[r][c]?.Value ?? 0;
            Assert.AreEqual(40, total);
        }

        // MARK: - Ajouts Unity

        /// Le cache rend une copie : une partie qui modifie la grille du jour
        /// n'altère pas celle du prochain appel.
        [Test]
        public void CachedGridIsCopiedForEachCaller()
        {
            DailyToday a = DailyChallenge.Make(20260910);
            a.Grid.RemoveBubbles(new[] { new Cell(0, 0) });
            DailyToday b = DailyChallenge.Make(20260910);
            Assert.IsNotNull(b.Grid.Cells[0][0]);
            Assert.AreEqual(6, b.Grid.Cells[0][0].Value);
        }

        [Test]
        public void TwistsArePlacedWithExpectedCounts()
        {
            // 20260629 : obstacles, 20260627 : ancrées, 20260628 : gelées.
            DailyToday obstacles = DailyChallenge.Make(20260629);
            int stones = 0, anchored = 0, frozen = 0;
            for (int r = 0; r < GameRules.Rows; r++)
                for (int c = 0; c < GameRules.Cols; c++)
                    if (obstacles.Grid.Blocked[r][c]) stones++;
            Assert.AreEqual(9, stones);
            DailyToday anchoredDay = DailyChallenge.Make(20260627);
            foreach (var row in anchoredDay.Grid.Cells) foreach (var b in row) if (b != null && b.IsAnchored) anchored++;
            Assert.AreEqual(6, anchored);
            DailyToday frozenDay = DailyChallenge.Make(20260628);
            foreach (var row in frozenDay.Grid.Cells) foreach (var b in row) if (b != null && b.IsFrozen) frozen++;
            Assert.AreEqual(6, frozen);
        }
    }
}
