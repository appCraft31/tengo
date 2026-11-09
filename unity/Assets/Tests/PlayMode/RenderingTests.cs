using System.Collections;
using NUnit.Framework;
using TenGo.Fx;
using TenGo.Haptics;
using TenGo.Rendering;
using TenGo.Showcase;
using TenGo.Ui;
using UnityEngine;
using UnityEngine.TestTools;

namespace TenGo.Tests
{
    /// Briques de rendu de l'étape 4 en jeu : création, états, animations pilotées
    /// par le `FxDriver`, effets, boutons, transition, vitrine complète.
    public class RenderingTests
    {
        Transform _root;

        [SetUp]
        public void SetUp()
        {
            FxTime.Reset();
            JuiceSettings.Reset();
            JuiceSettings.SystemReduceMotion = () => false;
            Look.Reset();
            HapticManager.Current = new NullHaptics();
            _root = new GameObject("TestRoot").transform;
        }

        [TearDown]
        public void TearDown()
        {
            Object.Destroy(_root.gameObject);
            FxTime.Reset();
            JuiceSettings.Reset();
            HapticManager.Reset();
        }

        static IEnumerator Seconds(float s)
        {
            float end = Time.realtimeSinceStartup + s;
            while (Time.realtimeSinceStartup < end) yield return null;
        }

        [UnityTest]
        public IEnumerator BubbleUsesShapeShaderAndThemeColor()
        {
            var b = BubbleView.Create(_root, 5, Vector2.zero);
            yield return null;
            var r = b.Circle.GetComponent<MeshRenderer>();
            Assert.AreEqual("TenGo/Shape", r.sharedMaterial.shader.name);
            Assert.AreEqual(Look.BubbleColor(5), b.Circle.Fill);
            Assert.AreEqual("5", b.Digit.Text);
            Assert.AreEqual(BubbleView.RadiusPt * 2f, b.Circle.SizePt.x, 1e-3f);
        }

        [UnityTest]
        public IEnumerator SelectionGrowsWithTensionAndRestores()
        {
            var b = BubbleView.Create(_root, 3, Vector2.zero);
            b.SetSelected(true, 1f);
            yield return Seconds(0.25f);
            Assert.That(b.transform.localScale.x, Is.GreaterThan(1.10f));
            Assert.AreEqual(Color.white.WithAlpha(0.9f), b.Circle.Stroke);
            b.SetSelected(false);
            yield return Seconds(0.25f);
            Assert.AreEqual(1f, b.transform.localScale.x, 0.01f);
        }

        [UnityTest]
        public IEnumerator PopShrinksToZeroAndCallsBack()
        {
            var b = BubbleView.Create(_root, 7, Vector2.zero);
            bool done = false;
            b.PlayPop(() => done = true);
            yield return Seconds(0.4f);
            Assert.IsTrue(done);
            Assert.IsTrue(b == null || b.transform.localScale.x < 0.01f);
        }

        [UnityTest]
        public IEnumerator FrozenAnchoredBloomAddChildren()
        {
            var b = BubbleView.Create(_root, 2, Vector2.zero);
            int before = b.transform.childCount;
            b.SetFrozen(true); b.SetAnchored(true); b.SetBloom(3);
            yield return null;
            Assert.Greater(b.transform.childCount, before + 2);
            Assert.IsTrue(b.IsFrozen && b.IsAnchored);
            Assert.AreEqual(3, b.Bloom);
            b.Thaw();
            yield return Seconds(0.4f);
            Assert.IsFalse(b.IsFrozen);
        }

        [UnityTest]
        public IEnumerator CommitLargeTriggersHitStopFlashAndHaptics()
        {
            var cam = new GameObject("Cam", typeof(Camera)).GetComponent<Camera>();
            cam.transform.SetParent(_root);
            cam.orthographic = true;
            cam.orthographicSize = Units.ReferenceOrthoSize;
            var fx = RenderShared.Node("Fx", _root);
            var juice = new JuiceDirector(fx, ShakeCamera.Attach(cam), Vitrine.ScenePt, new System.Random(1));
            var haptics = (NullHaptics)HapticManager.Current;
            juice.OnCommit(PopTier.Large, 6);
            Assert.AreEqual(0.04f, FxTime.Speed, 1e-6f, "arrêt sur image");
            Assert.IsNotNull(fx.Find("Flash"));
            yield return Seconds(0.2f);
            Assert.AreEqual(1f, FxTime.Speed, "rendu en temps réel");
            Assert.GreaterOrEqual(haptics.Impacts.Count, 3);
            juice.Burst(Vector2.zero, Color.red, PopTier.Medium);
            yield return Seconds(0.8f);
            Assert.AreEqual(Units.ReferenceOrthoSize, cam.orthographicSize, 1e-3f, "punch revenu");
            Assert.IsNull(fx.Find("Flash"));
        }

        [UnityTest]
        public IEnumerator ReducedEffectsSkipScreenShake()
        {
            JuiceSettings.ReducedPreference = () => true;
            JuiceSettings.InvalidateCache();
            var cam = new GameObject("Cam", typeof(Camera)).GetComponent<Camera>();
            cam.transform.SetParent(_root);
            var fx = RenderShared.Node("Fx", _root);
            var juice = new JuiceDirector(fx, ShakeCamera.Attach(cam), Vitrine.ScenePt);
            var start = cam.transform.localPosition;
            juice.OnCommit(PopTier.Large, 8);
            Assert.AreEqual(1f, FxTime.Speed, "pas d'arrêt sur image en mode calme");
            yield return Seconds(0.05f);
            Assert.AreEqual(start, cam.transform.localPosition);
        }

        [UnityTest]
        public IEnumerator ButtonHitTestAndTapFeedback()
        {
            int taps = 0;
            var b = UiButton.Create(_root, "B", Vector2.zero, new Vector2(200, 60), 30, null, "OK", NunitoWeight.Bold, 20, Color.black, 10, () => taps++);
            yield return null;
            Assert.IsTrue(b.Hit(Units.P(90, 20)));
            Assert.IsFalse(b.Hit(Units.P(120, 0)));
            Assert.IsTrue(b.Hit(Units.P(0, 21)), "zone tactile d'au moins 44 pt");
            b.Tap();
            Assert.AreEqual(1, taps);
            Assert.AreEqual(HapticStyle.Light, ((NullHaptics)HapticManager.Current).Impacts[0].style);
            Assert.IsNotNull(b.ReliefParts.Ledge);
        }

        [UnityTest]
        public IEnumerator SceneFaderSwapsUnderOpaqueVeil()
        {
            float alphaAtSwap = -1;
            Transform veil = null;
            veil = SceneFader.Fade(_root, Vitrine.ScenePt, () => alphaAtSwap = FxAlpha.Of(veil.gameObject).Alpha);
            yield return Seconds(0.45f);
            Assert.AreEqual(1f, alphaAtSwap, 1e-3f);
            Assert.IsTrue(veil == null);
        }

        [UnityTest]
        public IEnumerator EveryShowcasePageAndThemeBuildsWithoutErrors()
        {
            var cam = new GameObject("Cam", typeof(Camera)).GetComponent<Camera>();
            cam.transform.SetParent(_root);
            foreach (var theme in new[] { "default", "forest", "ocean", "desert", "candy", "space", "night" })
            {
                Look.Apply(theme, "classic", "classic");
                var page = RenderShared.Node(theme, _root);
                Vitrine.Build(Vitrine.Page.Grid, page, cam);
                yield return null;
                Object.Destroy(page.gameObject);
            }
            foreach (Vitrine.Page p in System.Enum.GetValues(typeof(Vitrine.Page)))
            {
                var page = RenderShared.Node(p.ToString(), _root);
                Vitrine.Build(p, page, cam);
                yield return Seconds(0.1f);
                Object.Destroy(page.gameObject);
            }
            LogAssert.NoUnexpectedReceived();
        }
    }
}
