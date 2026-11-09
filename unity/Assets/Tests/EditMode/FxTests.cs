using System.Collections.Generic;
using NUnit.Framework;
using TenGo.Fx;
using TenGo.Haptics;
using UnityEngine;

namespace TenGo.Tests
{
    /// Moteur d'animations, horloge, paliers d'effets, réglage « effets réduits », haptique.
    public class FxTests
    {
        [SetUp, TearDown]
        public void Clean()
        {
            FxTime.Reset();
            JuiceSettings.Reset();
            HapticManager.Reset();
        }

        [Test]
        public void SequenceCarriesLeftoverTimeAndParallelWaitsForLongest()
        {
            float a = 0, b = 0;
            var calls = new List<string>();
            var t = Tw.Seq(
                Tw.To(() => a, v => a = v, 1f, 0.1f),
                Tw.Call(() => calls.Add("mid")),
                Tw.Par(Tw.To(() => b, v => b = v, 2f, 0.2f), Tw.Wait(0.05f)),
                Tw.Call(() => calls.Add("end")));
            Assert.Less(t.Tick(0.15f), 0f);
            Assert.AreEqual(1f, a);
            Assert.AreEqual(0.5f, b, 1e-5f);
            CollectionAssert.AreEqual(new[] { "mid" }, calls);
            Assert.GreaterOrEqual(t.Tick(0.2f), 0f);
            Assert.AreEqual(2f, b);
            CollectionAssert.AreEqual(new[] { "mid", "end" }, calls);
            Assert.AreEqual(0.3f, t.Duration, 1e-6f);
        }

        [Test]
        public void ForeverRestartsAndEasingHitsEnds()
        {
            int n = 0;
            var t = Tw.Forever(Tw.Seq(Tw.Wait(0.1f), Tw.Call(() => n++)));
            for (int i = 0; i < 10; i++) t.Tick(0.1f);
            Assert.AreEqual(10, n);
            foreach (TwEase e in System.Enum.GetValues(typeof(TwEase)))
            {
                Assert.AreEqual(0f, TwEasing.Evaluate(e, 0f), 1e-5f, e.ToString());
                Assert.AreEqual(1f, TwEasing.Evaluate(e, 1f), 1e-5f, e.ToString());
            }
        }

        [Test]
        public void MoveByAccumulatesWithOtherMoves()
        {
            var go = new GameObject("t");
            try
            {
                var tr = go.transform;
                var m1 = Tw.MoveBy(tr, new Vector2(1, 0), 0.1f);
                var m2 = Tw.MoveBy(tr, new Vector2(0, 2), 0.2f);
                m1.Tick(0.1f); m2.Tick(0.2f);
                Assert.AreEqual(new Vector3(1, 2, 0), tr.localPosition);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void HitStopSlowsAnimationTimeButNotRealtime()
        {
            bool fired = false;
            FxTime.Speed = 0.04f;
            FxTime.AfterRealtime(0.06f, () => { FxTime.Speed = 1f; fired = true; });
            FxTime.Advance(0.05f, 0.01f);
            Assert.IsFalse(fired);
            Assert.AreEqual(0.002, FxTime.Now, 1e-5);
            FxTime.Advance(0.02f, 0.01f);
            Assert.IsTrue(fired);
            Assert.AreEqual(1f, FxTime.Speed);
            Assert.AreEqual(0.07, FxTime.RealNow, 1e-5);
        }

        [Test]
        public void KeyedRunReplacesAndDestroyedOwnerStops()
        {
            var go = new GameObject("t");
            int a = 0, b = 0;
            Tw.Run(go, Tw.Forever(Tw.Seq(Tw.Wait(0.01f), Tw.Call(() => a++))), "k");
            Tw.Run(go, Tw.Forever(Tw.Seq(Tw.Wait(0.01f), Tw.Call(() => b++))), "k");
            FxTime.Advance(0.05f, 0.01f);
            Assert.AreEqual(0, a);
            Assert.Greater(b, 0);
            Object.DestroyImmediate(go);
            int before = b;
            FxTime.Advance(0.05f, 0.01f);
            Assert.AreEqual(before, b);
        }

        [Test]
        public void PopTiersMatchIos()
        {
            Assert.AreEqual(PopTier.Small, PopTiers.ForLength(2));
            Assert.AreEqual(PopTier.Small, PopTiers.ForLength(3));
            Assert.AreEqual(PopTier.Medium, PopTiers.ForLength(4));
            Assert.AreEqual(PopTier.Medium, PopTiers.ForLength(5));
            Assert.AreEqual(PopTier.Large, PopTiers.ForLength(6));
            Assert.AreEqual(PopTier.Large, PopTier.Medium.Boosted());
            Assert.AreEqual(PopTier.Medium, PopTier.Small.Boosted());
            Assert.AreEqual(new[] { 16, 24, 34 }, new[] { PopTier.Small.BurstCount(), PopTier.Medium.BurstCount(), PopTier.Large.BurstCount() });
            Assert.AreEqual(9f, PopTier.Large.ShakeAmplitude());
            Assert.AreEqual(0.06f, PopTier.Large.HitStop(), 1e-6f);
            Assert.AreEqual(0f, PopTier.Medium.FlashAlpha());
            Assert.AreEqual(0.18f, PopTier.Large.FlashAlpha(), 1e-6f);
        }

        [Test]
        public void ReduceMotionCannotBeBypassedExceptByDemoMode()
        {
            bool system = false, pref = false;
            JuiceSettings.SystemReduceMotion = () => system;
            JuiceSettings.ReducedPreference = () => pref;
            Assert.IsTrue(JuiceSettings.MotionEnabled);
            pref = true; JuiceSettings.InvalidateCache();
            Assert.IsFalse(JuiceSettings.MotionEnabled);
            Assert.AreEqual(0.35f, JuiceSettings.ParticleScale);
            pref = false; system = true; JuiceSettings.InvalidateCache();
            Assert.IsFalse(JuiceSettings.MotionEnabled);
            Assert.IsTrue(JuiceSettings.IsForcedBySystem);
            JuiceSettings.ForceFull = true;
            Assert.IsTrue(JuiceSettings.MotionEnabled);
            Assert.IsFalse(JuiceSettings.IsForcedBySystem);
        }

        [Test]
        public void ChainHapticPatternFollowsLength()
        {
            var h = new NullHaptics();
            HapticManager.Current = h;
            HapticManager.Chain(3);
            Assert.AreEqual(1, h.Impacts.Count);
            Assert.AreEqual((HapticStyle.Medium, 0.7f), h.Impacts[0]);

            h.Impacts.Clear();
            HapticManager.Chain(6);
            Assert.AreEqual(1, h.Impacts.Count);
            FxTime.Advance(0.05f, 0.005f);
            Assert.AreEqual(2, h.Impacts.Count);
            Assert.AreEqual(HapticStyle.Rigid, h.Impacts[1].style);
            FxTime.Advance(0.05f, 0.005f);
            Assert.AreEqual(3, h.Impacts.Count);
            Assert.AreEqual((HapticStyle.Heavy, 1f), h.Impacts[2]);

            h.Impacts.Clear();
            HapticManager.IsEnabled = () => false;
            HapticManager.Chain(5);
            HapticManager.Light();
            FxTime.Advance(0.1f);
            Assert.AreEqual(0, h.Impacts.Count);
        }
    }
}
