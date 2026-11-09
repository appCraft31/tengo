using NUnit.Framework;
using TenGo.Core;

namespace TenGo.Tests
{
    /// Port de ScalesTest.kt (gamme de SoundManager.swift).
    public class ScalesTests
    {
        const double Eps = 0.01;

        [Test]
        public void ScaleIsAMajorPentatonicOverTwoOctaves()
        {
            double[] expected = { 220.0, 246.94, 277.18, 329.63, 369.99, 440.0, 493.88, 554.37, 659.26, 739.99 };
            Assert.AreEqual(10, Scales.Notes.Length);
            for (int i = 0; i < expected.Length; i++) Assert.AreEqual(expected[i], Scales.Notes[i], Eps, "note " + i);
        }

        [Test]
        public void BubbleValueMapsToFixedNote()
        {
            for (int v = 1; v <= 9; v++) Assert.AreEqual(v - 1, Scales.NoteIndex(v));
            Assert.AreEqual(220.0, Scales.FrequencyFor(1), Eps);
            Assert.AreEqual(659.26, Scales.FrequencyFor(9), Eps);
            Assert.AreEqual(Scales.FrequencyFor(7), Scales.FrequencyFor(7), 0.0);
        }

        [Test]
        public void OutOfRangeValuesAreClamped()
        {
            Assert.AreEqual(0, Scales.NoteIndex(0));
            Assert.AreEqual(0, Scales.NoteIndex(-3));
            Assert.AreEqual(9, Scales.NoteIndex(42));
        }
    }
}
