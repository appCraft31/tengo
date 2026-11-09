using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TenGo.Audio;
using TenGo.Core;

namespace TenGo.Tests
{
    /// Synthé temps réel et table des sons (SoundManager.swift).
    public class SynthTests
    {
        const int Rate = 44100;
        double _now;
        List<(double f, float v, bool s, double t)> _log;

        SoundBoard Make(out SynthEngine engine)
        {
            engine = new SynthEngine(Rate);
            _log = new List<(double, float, bool, double)>();
            engine.OnTrigger = (f, v, s, t) => _log.Add((f, v, s, t));
            _now = 10;
            return new SoundBoard(engine, () => _now);
        }

        [Test]
        public void SilenceRendersZeros()
        {
            var e = new SynthEngine(Rate);
            var pcm = e.RenderMono(0.2);
            Assert.AreEqual((int)(0.2 * Rate), pcm.Length);
            Assert.IsTrue(pcm.All(x => x == 0f));
        }

        [Test]
        public void NoteIsAudibleFiniteAndBelowFullScale()
        {
            var e = new SynthEngine(Rate);
            for (int i = 0; i < 8; i++) e.Trigger(Scales.Notes[i], 1f);
            var pcm = e.RenderMono(2.5);
            Assert.IsTrue(pcm.All(x => !float.IsNaN(x) && !float.IsInfinity(x)));
            double peak = WavWriter.PeakDbfs(pcm);
            Assert.Less(peak, 0.0, "écrêtage");
            Assert.Greater(peak, -30.0, "trop faible");
        }

        [Test]
        public void EightVoicesAtMost()
        {
            var e = new SynthEngine(Rate);
            for (int i = 0; i < 12; i++) e.Trigger(220 + i * 10, 1f);
            Assert.LessOrEqual(e.ActiveVoices, SynthEngine.VoiceCount);
        }

        [Test]
        public void ShortNoteDecaysBeforeLongNote()
        {
            var a = new SynthEngine(Rate); a.Trigger(440, 1f, isShort: true);
            var b = new SynthEngine(Rate); b.Trigger(440, 1f);
            a.RenderMono(1.0); b.RenderMono(1.0);
            Assert.AreEqual(0, a.ActiveVoices, "note courte (0,03/0,10/0,03/0,35 s) finie à 1 s");
            Assert.AreEqual(1, b.ActiveVoices, "note longue (relâche 2 s) encore active");
        }

        [Test]
        public void ImmediateNotesAreThrottledAt40ms()
        {
            var board = Make(out var e);
            board.PlaySelect(1);
            _now += 0.02; board.PlayConnect(2, 0.3);
            _now += 0.03; board.PlayConnect(3, 0.6);
            e.RenderMono(0.01);
            Assert.AreEqual(2, _log.Count);
            Assert.AreEqual(Scales.FrequencyFor(1), _log[0].f, 1e-9);
            Assert.AreEqual(Scales.FrequencyFor(3), _log[1].f, 1e-9);
            Assert.AreEqual(3, board.PathNotes.Count, "la note est retenue même étouffée");
        }

        [Test]
        public void ConnectJumpsAnOctaveFromTension08()
        {
            var board = Make(out var e);
            board.PlayConnect(4, 0.8);
            e.RenderMono(0.01);
            Assert.AreEqual(Scales.FrequencyFor(4) * 2, _log[0].f, 1e-9);
            Assert.AreEqual(0.61f, _log[0].v, 1e-5);
            Assert.IsTrue(_log[0].s);
        }

        [Test]
        public void ComboChordGrowsWithLengthAndIsStrummed()
        {
            foreach (var (len, notes) in new[] { (2, 2), (4, 3), (6, 4) })
            {
                var board = Make(out var e);
                board.PlaySelect(5);
                e.RenderMono(0.01);
                _log.Clear();
                double t0 = e.Time;
                board.PlayCombo(len);
                e.RenderMono(0.2);
                Assert.AreEqual(notes, _log.Count, "longueur " + len);
                double b = Scales.FrequencyFor(5);
                double[] ratios = { 1, 1.498, 2, 2.52 };
                for (int i = 0; i < notes; i++)
                {
                    Assert.AreEqual(b * ratios[i], _log[i].f, 1e-9);
                    Assert.AreEqual(t0 + i * SoundBoard.StrumInterval, _log[i].t, 1.5 / Rate);
                    Assert.AreEqual(0.55f, _log[i].v, 1e-6);
                }
                Assert.AreEqual(0, board.PathNotes.Count);
            }
        }

        [Test]
        public void WinPlaysThreeRisingNotesThenChord()
        {
            var board = Make(out var e);
            board.PlayWin();
            e.RenderMono(0.6);
            Assert.AreEqual(7, _log.Count);
            var n = Scales.Notes;
            Assert.AreEqual(n[4], _log[0].f, 1e-9);
            Assert.AreEqual(n[7], _log[1].f, 1e-9);
            Assert.AreEqual(n[9], _log[2].f, 1e-9);
            Assert.AreEqual(0.22, _log[2].t, 1.5 / Rate);
            Assert.IsTrue(_log.Skip(3).All(x => Math.Abs(x.t - 0.40) < 1.5 / Rate));
            Assert.AreEqual(n[5] * 2.52, _log[6].f, 1e-9);
        }

        [Test]
        public void MuteSilencesEverythingIncludingScheduledNotes()
        {
            var board = Make(out var e);
            board.PlayWin();
            board.IsMutedSource = () => true;
            board.PlaySelect(1);
            board.PlayLose();
            e.RenderMono(0.6);
            // Seule la première note de victoire (délai nul) est partie avant la coupure.
            Assert.AreEqual(1, _log.Count);
            Assert.AreEqual(Scales.Notes[4], _log[0].f, 1e-9);

            var silent = Make(out var e2);
            silent.IsMutedSource = () => true;
            silent.PlayWin(); silent.PlaySelect(1); silent.PlayCombo(6); silent.PlayLose();
            Assert.IsTrue(e2.RenderMono(0.6).All(x => x == 0f));
            Assert.AreEqual(0, _log.Count);
        }

        [Test]
        public void RejectedAndLoseNotes()
        {
            var board = Make(out var e);
            bool haptic = false;
            board.LoseHaptic = () => haptic = true;
            board.PlayRejected();
            board.PlayLose();
            e.RenderMono(0.01);
            Assert.AreEqual(110.0, _log[0].f, 1e-9);
            Assert.AreEqual(0.30f, _log[0].v, 1e-6);
            Assert.AreEqual(98.0, _log[1].f, 1e-9);
            Assert.IsFalse(_log[1].s);
            Assert.IsTrue(haptic);
        }

        [Test]
        public void StereoRenderDuplicatesMono()
        {
            var e = new SynthEngine(Rate);
            e.Trigger(330, 1f);
            var buf = new float[512 * 2];
            e.Render(buf, 2);
            for (int i = 0; i < 512; i++) Assert.AreEqual(buf[2 * i], buf[2 * i + 1]);
            Assert.IsTrue(buf.Any(x => x != 0f));
        }
    }
}
