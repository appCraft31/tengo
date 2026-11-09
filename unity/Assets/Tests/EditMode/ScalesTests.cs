using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TenGo.Core;

namespace TenGo.Tests
{
    /// Gammes tournantes du son 4.0 (render.py, variante B).
    public class ScalesTests
    {
        const double Eps = 0.01;

        [Test]
        public void FirstPalettesAreThoseOfTheValidatedMockup()
        {
            // Variante B : Sol 196 Hz majeur, puis Si♭ 233,08 Hz mineur.
            double[] gMajor = { 196.00, 220.00, 246.94, 293.66, 329.63, 392.00, 440.00, 493.88, 587.33, 659.26, 784.00, 880.00, 987.77, 1174.66, 1318.51 };
            double[] bbMinor = { 233.08, 277.18, 311.13, 349.23, 415.30, 466.16, 554.37, 622.25, 698.46, 830.61, 932.33, 1108.73, 1244.51, 1396.91, 1661.22 };
            Assert.AreEqual(Scales.NoteCount, Scales.NotesFor(0).Length);
            for (int i = 0; i < gMajor.Length; i++) Assert.AreEqual(gMajor[i], Scales.NotesFor(0)[i], Eps, "Sol majeur " + i);
            for (int i = 0; i < bbMinor.Length; i++) Assert.AreEqual(bbMinor[i], Scales.NotesFor(1)[i], Eps, "Si♭ mineur " + i);
        }

        [Test]
        public void PalettesAlternateMajorMinorWithRootsInRange()
        {
            Assert.AreEqual(10, Scales.Palettes.Length);
            for (int p = 0; p < Scales.Palettes.Length; p++)
            {
                var pal = Scales.Palettes[p];
                Assert.AreEqual(p % 2 == 1, pal.Minor, pal.Name);
                Assert.That(pal.Root, Is.InRange(174.0, 247.0), pal.Name);
                Assert.AreEqual(pal.Root, Scales.NotesFor(p)[0], 1e-9);
            }
            // Chaque couple (racine, gamme) n'apparaît qu'une fois.
            Assert.AreEqual(10, Scales.Palettes.Select(x => (x.RootSemitones, x.Minor)).Distinct().Count());
        }

        [Test]
        public void EveryPaletteIsAPentatonicAscending()
        {
            for (int p = 0; p < Scales.Palettes.Length; p++)
            {
                var n = Scales.NotesFor(p);
                var offsets = Scales.Palettes[p].Minor ? Scales.MinorOffsets : Scales.MajorOffsets;
                for (int i = 0; i < n.Length; i++)
                {
                    double semis = 12 * System.Math.Log(n[i] / n[0], 2);
                    Assert.AreEqual(offsets[i % 5] + 12 * (i / 5), semis, 1e-6, $"palette {p} note {i}");
                }
            }
        }

        [Test]
        public void BubbleValueMapsToOneNoteOfTheCurrentPalette()
        {
            for (int p = 0; p < Scales.Palettes.Length; p++)
                for (int v = 1; v <= 9; v++)
                {
                    Assert.AreEqual(v - 1, Scales.NoteIndex(v));
                    Assert.AreEqual(Scales.NotesFor(p)[v - 1], Scales.FrequencyFor(v, p), 0.0);
                }
            Assert.AreEqual(196.0, Scales.FrequencyFor(1, 0), Eps);
            Assert.AreEqual(233.08, Scales.FrequencyFor(1, 1), Eps);
        }

        [Test]
        public void OutOfRangeValuesAreClamped()
        {
            Assert.AreEqual(0, Scales.NoteIndex(0));
            Assert.AreEqual(0, Scales.NoteIndex(-3));
            Assert.AreEqual(9, Scales.NoteIndex(42));
            Assert.AreEqual(Scales.NotesFor(3), Scales.NotesFor(13));
            Assert.AreEqual(Scales.NotesFor(9), Scales.NotesFor(-1));
        }

        [Test]
        public void SharedGridAlwaysGetsTheSamePalette()
        {
            var grid = GridModel.Generate(new SeededGenerator(20261109));
            var same = GridModel.Generate(new SeededGenerator(20261109));
            ulong h = Scales.GridHash(grid);
            Assert.AreEqual(h, Scales.GridHash(same));
            int expected = (int)(h % 10);
            // Quelle que soit la partie précédente : défi du jour et duels sonnent pareil pour tous.
            for (int prev = -1; prev < 10; prev++) Assert.AreEqual(expected, Scales.PaletteForGame(h, true, prev));
        }

        [Test]
        public void RandomGridsNeverRepeatThePreviousPalette()
        {
            var rng = new SeededGenerator(42);
            int prev = -1;
            var seen = new HashSet<int>();
            for (int game = 0; game < 200; game++)
            {
                ulong h = Scales.GridHash(GridModel.Generate(rng));
                int p = Scales.PaletteForGame(h, false, prev);
                Assert.AreNotEqual(prev, p, "partie " + game);
                Assert.AreEqual(p, Scales.PaletteForGame(h, false, prev), "déterministe");
                seen.Add(p);
                prev = p;
            }
            Assert.AreEqual(10, seen.Count, "toutes les palettes finissent par sortir");
        }

        [Test]
        public void GridHashDependsOnValues()
        {
            var a = GridModel.Generate(new SeededGenerator(1));
            var b = GridModel.Generate(new SeededGenerator(2));
            Assert.AreNotEqual(Scales.GridHash(a), Scales.GridHash(b));
            Assert.AreEqual(Scales.GridHash(a), Scales.GridHash(a.Copy()));
        }
    }
}
