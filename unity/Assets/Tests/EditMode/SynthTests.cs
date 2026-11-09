using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TenGo.Audio;
using TenGo.Core;

namespace TenGo.Tests
{
    /// Synthé temps réel et table des sons (son 4.0, render.py variante B).
    public class SynthTests
    {
        const int Rate = 44100;
        const double OneSample = 1.5 / Rate;
        double _now;
        List<(double f, float v, VoiceKind k, double t)> _log;

        SoundBoard Make(out SynthEngine engine, bool humanize = false, int palette = 0)
        {
            engine = new SynthEngine(Rate);
            _log = new List<(double, float, VoiceKind, double)>();
            engine.OnTrigger = (f, v, k, t) => _log.Add((f, v, k, t));
            _now = 10;
            var board = new SoundBoard(engine, () => _now, seed: 1) { Humanize = humanize };
            board.SetPalette(palette);
            return board;
        }

        static double Cents(double f, double reference) => 1200 * Math.Log(f / reference, 2);

        // MARK: - Moteur

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
            for (int i = 0; i < SynthEngine.VoiceCount; i++) e.Trigger(VoiceKind.KalimbaLong, Scales.NotesFor(0)[i], 1f);
            var pcm = e.RenderMono(2.5);
            Assert.IsTrue(pcm.All(x => !float.IsNaN(x) && !float.IsInfinity(x)));
            double peak = WavWriter.PeakDbfs(pcm);
            Assert.Less(peak, 0.0, "écrêtage");
            Assert.Greater(peak, -30.0, "trop faible");
        }

        [Test]
        public void VoiceCountIsBounded()
        {
            var e = new SynthEngine(Rate);
            for (int i = 0; i < 20; i++) e.Trigger(220 + i * 10, 1f);
            Assert.AreEqual(SynthEngine.VoiceCount, e.ActiveVoices);
        }

        [Test]
        public void ShortNoteDecaysBeforeLongNote()
        {
            var a = new SynthEngine(Rate); a.Trigger(440, 1f, isShort: true);
            var b = new SynthEngine(Rate); b.Trigger(440, 1f);
            a.RenderMono(1.3); b.RenderMono(1.3);
            Assert.AreEqual(0, a.ActiveVoices, "kalimba courte (1,2 s) finie");
            Assert.AreEqual(1, b.ActiveVoices, "kalimba longue (2,4 s) encore active");
            b.RenderMono(1.2);
            Assert.AreEqual(0, b.ActiveVoices);
        }

        [Test]
        public void KalimbaHasTineAndClickThatDieQuickly()
        {
            // Corps en exp −5,5/s : à 0,5 s il reste ~6 % ; la lame (−28/s) et le clic (−90/s) ont disparu.
            var v = new SynthVoice(Rate);
            v.NoteOn(VoiceKind.KalimbaShort, 200, 1, 1f);
            var x = new float[Rate];
            for (int i = 0; i < x.Length; i++) x[i] = v.NextSample();
            double Rms(int from, int len) => Math.Sqrt(x.Skip(from).Take(len).Sum(s => (double)s * s) / len);
            double early = Rms(0, 441), late = Rms(Rate / 2, 441);
            Assert.Greater(early, 0.25);
            Assert.AreEqual(Math.Exp(-5.5 * 0.5) * SynthVoice.KalimbaGain / Math.Sqrt(2), late, 0.01);
        }

        [Test]
        public void VoiceStealingDoesNotClick()
        {
            var e = new SynthEngine(Rate);
            for (int i = 0; i < SynthEngine.VoiceCount; i++) e.Trigger(VoiceKind.KalimbaLong, 300 + i * 37, 1f);
            var before = e.RenderMono(0.05);
            for (int i = 0; i < SynthEngine.VoiceCount; i++) e.Trigger(VoiceKind.KalimbaLong, 900 + i * 13, 1f);
            var after = e.RenderMono(0.05);
            double jump = Math.Abs(after[0] - before[^1]);
            double typical = before.Zip(before.Skip(1), (a, b) => Math.Abs(b - a)).Max();
            Assert.LessOrEqual(jump, typical * 1.5, "saut au vol de voix");
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

        [Test]
        public void SoftLimiterIsTransparentThenBounded()
        {
            Assert.AreEqual(0.5f, SoftLimiter.Process(0.5f));
            Assert.AreEqual(-0.8f, SoftLimiter.Process(-0.8f));
            Assert.LessOrEqual(SoftLimiter.Process(5f), SoftLimiter.Ceiling);
            Assert.Less(SoftLimiter.Process(-5f), 0f);
            Assert.Greater(SoftLimiter.Process(1.2f), SoftLimiter.Process(1.0f));
        }

        // MARK: - Table des sons

        [Test]
        public void EachDigitPlaysItsNoteInTheCurrentPalette()
        {
            for (int p = 0; p < Scales.Palettes.Length; p++)
            {
                var board = Make(out var e, palette: p);
                for (int v = 1; v <= 9; v++) { board.PlaySelect(v); _now += 0.1; }
                e.RenderMono(0.01);
                Assert.AreEqual(9, _log.Count);
                for (int v = 1; v <= 9; v++)
                {
                    Assert.AreEqual(Scales.FrequencyFor(v, p), _log[v - 1].f, 1e-9, $"palette {p} chiffre {v}");
                    Assert.AreEqual(VoiceKind.KalimbaShort, _log[v - 1].k);
                }
            }
        }

        [Test]
        public void BeginGameRotatesPaletteAndKeepsNotesFixedWithinAGame()
        {
            var board = Make(out var e);
            var rng = new SeededGenerator(3);
            int prev = -1;
            for (int game = 0; game < 20; game++)
            {
                var grid = GridModel.Generate(rng);
                int p = board.BeginGame(Scales.GridHash(grid), sharedGrid: false);
                Assert.AreNotEqual(prev, p);
                Assert.AreEqual(p, board.Palette);
                Assert.AreEqual(Scales.FrequencyFor(7, p), board.FrequencyFor(7));
                prev = p;
            }
            // Grille partagée : même palette pour tous, quelle que soit l'histoire.
            var daily = GridModel.Generate(new SeededGenerator(99));
            var other = Make(out _);
            Assert.AreEqual(board.BeginGame(Scales.GridHash(daily), true), other.BeginGame(Scales.GridHash(daily), true));
        }

        [Test]
        public void HumanizationStaysWithin8CentsAndVelocityJitter()
        {
            var board = Make(out var e, humanize: true);
            var detunes = new List<double>();
            for (int i = 0; i < 300; i++) { board.PlayConnect(5, 0.5); _now += 0.1; }
            e.RenderMono(0.01);
            Assert.AreEqual(300, _log.Count);
            foreach (var x in _log)
            {
                double c = Cents(x.f, Scales.FrequencyFor(5, 0));
                Assert.That(c, Is.InRange(-8.0001, 8.0001));
                Assert.That(x.v, Is.InRange(0.55f * 0.85f - 1e-5f, 0.55f * 1.05f + 1e-5f));
                detunes.Add(c);
            }
            Assert.Greater(detunes.Max() - detunes.Min(), 10, "les notes varient bien");
        }

        [Test]
        public void FirstBubbleIsAt80PercentVelocity()
        {
            var board = Make(out var e);
            board.PlaySelect(3);
            e.RenderMono(0.01);
            Assert.AreEqual(0.36f, _log[0].v, 1e-5);
        }

        [Test]
        public void ImmediateNotesAreThrottledAt70ms()
        {
            var board = Make(out var e);
            board.PlaySelect(1);
            _now += 0.04; board.PlayConnect(2, 0.3);   // 40 ms : étouffée
            _now += 0.035; board.PlayConnect(3, 0.6);  // 75 ms après la première : jouée
            _now += 0.069; board.PlayConnect(4, 0.7);  // 69 ms : étouffée
            e.RenderMono(0.01);
            Assert.AreEqual(2, _log.Count);
            Assert.AreEqual(Scales.FrequencyFor(1, 0), _log[0].f, 1e-9);
            Assert.AreEqual(Scales.FrequencyFor(3, 0), _log[1].f, 1e-9);
            Assert.AreEqual(4, board.PathNotes.Count, "la note est retenue même étouffée");
        }

        [Test]
        public void ConnectJumpsAnOctaveFromTension08()
        {
            var board = Make(out var e);
            board.PlayConnect(4, 0.8);
            e.RenderMono(0.01);
            Assert.AreEqual(Scales.FrequencyFor(4, 0) * 2, _log[0].f, 1e-9);
            Assert.AreEqual(0.61f, _log[0].v, 1e-5);
            Assert.AreEqual(VoiceKind.KalimbaShort, _log[0].k);
        }

        [Test]
        public void ComboArpeggioLengthStepAndResolutionFollowChainLength()
        {
            for (int len = 2; len <= 9; len++)
            {
                var board = Make(out var e, palette: 1);
                board.PlaySelect(5);
                e.RenderMono(0.01);
                _log.Clear();
                double t0 = e.Time;
                board.PlayCombo(len);
                e.RenderMono(1.0);
                var N = Scales.NotesFor(1);
                int notes = Math.Max(2, Math.Min(7, len));
                Assert.AreEqual(notes, SoundBoard.ArpeggioLength(len));
                var arp = _log.Where(x => x.k == VoiceKind.KalimbaLong).ToList();
                var bells = _log.Where(x => x.k == VoiceKind.Bell).ToList();
                Assert.AreEqual(notes, arp.Count, "longueur " + len);
                double step = len >= 5 ? 0.060 : 0.045;
                for (int i = 0; i < notes; i++)
                {
                    Assert.AreEqual(t0 + i * step, arp[i].t, OneSample, $"chaîne {len} note {i}");
                    Assert.AreEqual(0.5f, arp[i].v, 1e-6);
                    if (i > 0 && i < notes - 1) Assert.GreaterOrEqual(arp[i].f, arp[i - 1].f, "arpège montant (plafonné à la dernière note)");
                }
                // Résolution sur la tonique, une octave plus haut à partir de 6.
                Assert.AreEqual(N[SoundBoard.Tonic] * (len >= 6 ? 2 : 1), arp[^1].f, 1e-9);
                Assert.AreEqual(len >= 6 ? 1 : 0, bells.Count, "cloche à partir de 6");
                if (len >= 6)
                {
                    Assert.AreEqual(N[10], bells[0].f, 1e-9);
                    Assert.AreEqual(t0 + notes * 0.060, bells[0].t, OneSample);
                }
                Assert.AreEqual(0, board.PathNotes.Count);
            }
        }

        [Test]
        public void ComboStartsFromTheMiddleOctave()
        {
            var board = Make(out var e);
            var N = Scales.NotesFor(0);
            board.PlayCombo(4);
            e.RenderMono(0.5);
            CollectionAssert.AreEqual(new[] { N[5], N[7], N[9], N[5] }, _log.Select(x => x.f).ToArray());
            _log.Clear();
            board.PlayCombo(5);
            e.RenderMono(0.5);
            CollectionAssert.AreEqual(new[] { N[7], N[9], N[11], N[12], N[5] }, _log.Select(x => x.f).ToArray());
        }

        [Test]
        public void WinPlaysSevenNoteArpeggioThenThreeRisingBells()
        {
            var board = Make(out var e);
            board.PlayWin();
            e.RenderMono(1.0);
            var N = Scales.NotesFor(0);
            Assert.AreEqual(10, _log.Count);
            int[] arp = { 5, 6, 7, 8, 9, 10, 12 };
            for (int i = 0; i < 7; i++)
            {
                Assert.AreEqual(N[arp[i]], _log[i].f, 1e-9);
                Assert.AreEqual(i * 0.07, _log[i].t, OneSample);
                Assert.AreEqual(VoiceKind.KalimbaLong, _log[i].k);
            }
            int[] bells = { 10, 12, 14 };
            for (int i = 0; i < 3; i++)
            {
                Assert.AreEqual(N[bells[i]], _log[7 + i].f, 1e-9);
                Assert.AreEqual(0.55 + i * 0.12, _log[7 + i].t, OneSample);
                Assert.AreEqual(VoiceKind.Bell, _log[7 + i].k);
            }
        }

        [Test]
        public void LoseIsThreeDescendingNotesWithLightHaptic()
        {
            var board = Make(out var e);
            bool haptic = false;
            board.LoseHaptic = () => haptic = true;
            board.PlayLose();
            e.RenderMono(0.6);
            var N = Scales.NotesFor(0);
            Assert.IsTrue(haptic);
            Assert.AreEqual(3, _log.Count, "pas de note grave isolée");
            CollectionAssert.AreEqual(new[] { N[4], N[2], N[0] }, _log.Select(x => x.f).ToArray());
            for (int i = 0; i < 3; i++) Assert.AreEqual(i * 0.22, _log[i].t, OneSample);
            Assert.IsTrue(_log.All(x => x.k == VoiceKind.KalimbaLong && Math.Abs(x.v - 0.45f) < 1e-6));
        }

        [Test]
        public void RejectedIsAMuffledTok()
        {
            var board = Make(out var e);
            board.PlayRejected();
            var pcm = e.RenderMono(0.3);
            Assert.AreEqual(1, _log.Count);
            Assert.AreEqual(150.0, _log[0].f, 1e-9);
            Assert.AreEqual(VoiceKind.Tok, _log[0].k);
            Assert.AreEqual(0, e.ActiveVoices, "tok de 120 ms");
            Assert.IsTrue(pcm.Any(x => Math.Abs(x) > 0.01f));
        }

        [Test]
        public void MuteSilencesEverythingIncludingScheduledNotes()
        {
            var board = Make(out var e);
            board.PlayWin();
            board.IsMutedSource = () => true;
            board.PlaySelect(1);
            board.PlayLose();
            e.RenderMono(1.0);
            // Seule la première note de victoire (délai nul) est partie avant la coupure.
            Assert.AreEqual(1, _log.Count);
            Assert.AreEqual(Scales.NotesFor(0)[5], _log[0].f, 1e-9);

            var silent = Make(out var e2);
            silent.IsMutedSource = () => true;
            silent.PlayWin(); silent.PlaySelect(1); silent.PlayCombo(6); silent.PlayRejected(); silent.PlayLose();
            Assert.IsTrue(e2.RenderMono(1.0).All(x => x == 0f));
            Assert.AreEqual(0, _log.Count);
        }

        // MARK: - Séquence type (render.py)

        [Test]
        public void TypicalSequenceNeverSaturates()
        {
            var triggers = 0;
            var pcm = SoundSequence.Render(Rate, onTrigger: (f, v, k, t) => triggers++);
            Assert.AreEqual((int)Math.Round(SoundSequence.Duration * Rate), pcm.Length);
            Assert.IsTrue(pcm.All(x => !float.IsNaN(x) && !float.IsInfinity(x)));
            float peak = pcm.Max(x => Math.Abs(x));
            Assert.Less(peak, SoftLimiter.Threshold, "le limiteur ne doit pas travailler sur une partie normale");
            double rms = Math.Sqrt(pcm.Sum(x => (double)x * x) / pcm.Length);
            Assert.That(20 * Math.Log10(rms), Is.InRange(-27.0, -16.0), "niveau perçu proche de l'ancien son (−21,6 dBFS)");
            Assert.Greater(triggers, 100);
        }
    }
}
