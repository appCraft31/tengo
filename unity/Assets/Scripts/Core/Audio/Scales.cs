#nullable enable
using System;

namespace TenGo.Core
{
    /// Gamme FIXE du jeu (SoundManager.swift, Scales.kt) : un 7 sonne toujours
    /// pareil, l'oreille apprend la grille. Pentatonique majeure de La 220 Hz
    /// sur deux octaves : 10 notes pour les valeurs 1 à 9, aucune dissonance possible.
    public static class Scales
    {
        /// Pentatonique majeure (décalages en demi-tons).
        private static readonly int[] Offsets = { 0, 2, 4, 7, 9 };

        /// Racine : La 220 Hz.
        public const double Root = 220.0;

        public static readonly double[] Notes = Build();

        private static double[] Build()
        {
            var notes = new double[Offsets.Length * 2];
            int i = 0;
            for (int octave = 0; octave < 2; octave++)
                foreach (var offset in Offsets)
                    notes[i++] = Root * Math.Pow(2.0, (octave * 12 + offset) / 12.0);
            return notes;
        }

        /// Valeur d'une bulle (1–9) → index sûr dans la gamme.
        public static int NoteIndex(int bubbleValue) => Math.Max(0, Math.Min(bubbleValue - 1, Notes.Length - 1));

        /// Fréquence de la bulle de valeur `bubbleValue`.
        public static double FrequencyFor(int bubbleValue) => Notes[NoteIndex(bubbleValue)];
    }
}
