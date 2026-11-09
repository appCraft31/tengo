#nullable enable
using System;

namespace TenGo.Core
{
    /// Gammes TOURNANTES du jeu (son 4.0, décision du 9 nov. 2026) : à chaque
    /// nouvelle partie, la tonalité ET la gamme changent (pentatonique majeure
    /// ou mineure, racine entre Fa 174,6 Hz et Si♭ 233,1 Hz). Dans une partie,
    /// un chiffre garde toujours sa note : la valeur v joue `Notes[v − 1]` de la
    /// palette courante. Pentatonique : aucune dissonance possible.
    ///
    /// Règle de rotation (`PaletteForGame`) : la palette se déduit de la grille
    /// de départ (`GridHash`). Une grille partagée (défi du jour, duel, puzzle,
    /// démo) sonne donc pareil pour tout le monde et sur toutes les plateformes ;
    /// une grille aléatoire (partie classique, Rush, reprise) qui tomberait sur
    /// la palette de la partie précédente passe à la suivante, pour que le son
    /// change bien d'une partie à l'autre.
    public static class Scales
    {
        /// La 220 Hz, référence des racines.
        public const double A3 = 220.0;

        /// Pentatonique majeure (décalages en demi-tons).
        public static readonly int[] MajorOffsets = { 0, 2, 4, 7, 9 };
        /// Pentatonique mineure.
        public static readonly int[] MinorOffsets = { 0, 3, 5, 7, 10 };

        /// Notes par palette : trois octaves (l'arpège de victoire et les cloches
        /// montent jusqu'à `Notes[14]`).
        public const int NoteCount = 15;

        /// Notes jouables par une bulle (1–9, plus une de marge comme avant).
        public const int BubbleNoteCount = 10;

        /// Les dix palettes, dans un ordre qui alterne majeur et mineur. La
        /// variante B validée commence par Sol majeur puis Si♭ mineur.
        public static readonly ScalePalette[] Palettes =
        {
            new("Sol majeur", -2, false),
            new("Si♭ mineur", 1, true),
            new("La majeur", 0, false),
            new("Fa mineur", -4, true),
            new("Si♭ majeur", 1, false),
            new("Sol mineur", -2, true),
            new("Fa majeur", -4, false),
            new("La mineur", 0, true),
            new("La♭ majeur", -1, false),
            new("La♭ mineur", -1, true),
        };

        static readonly double[][] _notes = BuildAll();

        static double[][] BuildAll()
        {
            var all = new double[Palettes.Length][];
            for (int p = 0; p < Palettes.Length; p++)
            {
                var pal = Palettes[p];
                var offsets = pal.Minor ? MinorOffsets : MajorOffsets;
                var notes = new double[NoteCount];
                int i = 0;
                for (int octave = 0; i < NoteCount; octave++)
                    foreach (var o in offsets)
                        if (i < NoteCount) notes[i++] = A3 * Math.Pow(2.0, (pal.RootSemitones + octave * 12 + o) / 12.0);
                all[p] = notes;
            }
            return all;
        }

        /// Index de palette ramené dans [0, Palettes.Length).
        public static int Wrap(int palette) => ((palette % Palettes.Length) + Palettes.Length) % Palettes.Length;

        /// Les 15 notes de la palette `palette` (tableau partagé, ne pas modifier).
        public static double[] NotesFor(int palette) => _notes[Wrap(palette)];

        /// Valeur d'une bulle (1–9) → index sûr dans la gamme.
        public static int NoteIndex(int bubbleValue) => Math.Max(0, Math.Min(bubbleValue - 1, BubbleNoteCount - 1));

        /// Fréquence de la bulle de valeur `bubbleValue` dans la palette `palette`.
        public static double FrequencyFor(int bubbleValue, int palette) => NotesFor(palette)[NoteIndex(bubbleValue)];

        /// Palette d'une nouvelle partie (voir la règle en tête de classe).
        /// `previous` : palette de la partie précédente (−1 s'il n'y en a pas).
        public static int PaletteForGame(ulong gridHash, bool sharedGrid, int previous)
        {
            int p = (int)(gridHash % (ulong)Palettes.Length);
            if (!sharedGrid && p == previous) p = Wrap(p + 1);
            return p;
        }

        /// Empreinte stable d'une grille (valeurs des bulles, cases bloquées),
        /// FNV-1a 64 bits puis mélange SplitMix64 : même grille, même empreinte.
        public static ulong GridHash(GridModel grid)
        {
            ulong h = 14695981039346656037UL;
            void Mix(int v) { h ^= (uint)v; h *= 1099511628211UL; }
            for (int r = 0; r < grid.Cells.Length; r++)
            {
                var row = grid.Cells[r];
                for (int c = 0; c < row.Length; c++)
                {
                    Mix(row[c]?.Value ?? 0);
                    Mix(grid.Blocked[r][c] ? 1 : 0);
                }
                Mix(-1);
            }
            h ^= h >> 30; h *= 0xBF58476D1CE4E5B9UL;
            h ^= h >> 27; h *= 0x94D049BB133111EBUL;
            h ^= h >> 31;
            return h;
        }
    }
}
