using System;
using System.Collections.Generic;
using NUnit.Framework;
using TenGo.Core;

namespace TenGo.Tests
{
    /// Premier lancement (FirstRun.swift, TEN-4) : ajouts Unity.
    public class FirstRunTests
    {
        private sealed class RecordingAnalytics : IGameAnalytics
        {
            public readonly List<string> Events = new List<string>();
            public void LevelEnd(string mode, int score, bool won) => Events.Add("level_end");
            public void DailyChallengePlayed(int score) => Events.Add("daily_challenge_played");
            public void MirrorInsightShown(string kind) => Events.Add("mirror_insight_shown:" + kind);
            public void SeedPlanted(int power, bool strengthened) => Events.Add("seed_planted");
            public void FirstGameStart(string mode, long seconds) => Events.Add("first_game_start:" + mode + ":" + seconds);
            public void FirstGameComplete(string mode, int score, bool won, long seconds) => Events.Add("first_game_complete:" + mode + ":" + score + ":" + seconds);
        }

        [Test]
        public void NewInstallMeasuresFirstGameOnce()
        {
            var store = new MemorySaveStore();
            var clock = new FixedClock(new DateTimeOffset(2026, 11, 9, 12, 0, 0, TimeSpan.Zero));
            var analytics = new RecordingAnalytics();
            var firstRun = new FirstRunState(store, clock, analytics);
            firstRun.Bootstrap();
            Assert.IsTrue(firstRun.IsFirstSession);
            Assert.IsTrue(firstRun.IsPending);
            clock.Advance(TimeSpan.FromSeconds(42));
            firstRun.GameStarted("normal");
            firstRun.GameStarted("normal");
            clock.Advance(TimeSpan.FromSeconds(100));
            Assert.IsTrue(firstRun.GameCompleted("normal", 300, false));
            Assert.IsFalse(firstRun.GameCompleted("normal", 300, false));
            CollectionAssert.AreEqual(new[] { "first_game_start:normal:42", "first_game_complete:normal:300:142" }, analytics.Events);
            Assert.IsTrue(firstRun.IsFirstSession, "reste vrai jusqu'au prochain lancement");
            var relaunch = new FirstRunState(store, clock, analytics);
            relaunch.Bootstrap();
            Assert.IsFalse(relaunch.IsFirstSession);
        }

        [Test]
        public void ExistingPlayerIsNotMeasured()
        {
            var store = new MemorySaveStore();
            store.SetIntList(SaveKeys.HighScores, new[] { 500 });
            var analytics = new RecordingAnalytics();
            var firstRun = new FirstRunState(store, new FixedClock(DateTimeOffset.UnixEpoch), analytics);
            firstRun.Bootstrap();
            Assert.IsFalse(firstRun.IsPending);
            Assert.AreEqual("done", store.GetString(SaveKeys.FirstRunState));
            firstRun.GameStarted("normal");
            Assert.IsFalse(firstRun.GameCompleted("normal", 1, true));
            Assert.IsEmpty(analytics.Events);
        }
    }
}
