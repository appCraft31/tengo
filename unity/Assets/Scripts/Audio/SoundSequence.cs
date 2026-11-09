using System;
using System.Collections.Generic;
using System.Globalization;

namespace TenGo.Audio
{
    /// Séquence de jeu type de la maquette sonore (render.py, deux parties :
    /// une gagnée en Sol majeur, une perdue en Si♭ mineur), rejouée hors ligne
    /// par le vrai moteur. Sert au rendu de contrôle (`TenGo.Editor.SoundPreview`)
    /// et au test de non-saturation. Les instants sont ceux de render.py
    /// (graine 7) ; format : « t s v » sélection, « t c v tension » bulle
    /// suivante, « t r » refus, « t k n » chaîne de n, « t w » victoire, « t l » défaite.
    public static class SoundSequence
    {
        public const double Duration = 42.2055;

        /// Palettes des deux parties (Sol majeur puis Si♭ mineur).
        public static readonly int[] GamePalettes = { 0, 1 };

        static readonly string[] Games =
        {
            "0.50000 s 4;0.68001 c 6 1.00;0.93178 k 2;2.34720 s 3;2.49521 c 7 1.00;2.69923 k 2;4.17336 s 1;4.30378 c 2 0.30;4.49948 c 3 0.60;4.69324 c 4 1.00;4.91068 k 4;6.04250 r;6.94250 s 5;7.09477 c 5 1.00;7.29516 k 2;8.51221 s 2;8.68257 c 2 0.40;8.85685 c 2 0.60;9.06649 c 4 1.00;9.30990 k 4;10.63321 s 1;10.84233 c 1 0.20;10.98955 c 1 0.30;11.13237 c 3 0.60;11.31137 c 4 1.00;11.49489 k 5;12.46630 s 9;12.63749 c 1 1.00;12.85478 k 2;14.35508 s 2;14.53542 c 3 0.50;14.70655 c 5 1.00;14.92630 k 3;16.02481 s 1;16.15575 c 1 0.20;16.30115 c 2 0.40;16.48651 c 2 0.60;16.63256 c 1 0.70;16.79212 c 3 1.00;16.97242 k 6;18.42045 w",
            "22.42045 s 6;22.56280 c 4 1.00;22.76421 k 2;24.24241 s 2;24.41320 c 8 1.00;24.66097 k 2;25.99480 s 3;26.18414 c 3 0.60;26.32146 c 4 1.00;26.54475 k 3;27.79941 s 1;27.99912 c 4 0.50;28.15802 c 5 1.00;28.38588 k 3;29.37143 s 7;29.53244 c 3 1.00;29.73828 k 2;30.77840 r;31.67840 s 2;31.87371 c 2 0.40;32.03406 c 1 0.50;32.24236 c 1 0.60;32.41956 c 4 1.00;32.64797 k 5;33.98077 s 5;34.16488 c 1 0.60;34.30694 c 4 1.00;34.52217 k 3;35.61591 s 1;35.77811 c 1 0.20;35.91584 c 1 0.30;36.12327 c 1 0.40;36.27047 c 1 0.50;36.45421 c 2 0.70;36.60825 c 3 1.00;36.85817 k 7;38.20550 l",
        };

        /// Rend la séquence en mono à `sampleRate` Hz. `seed` : humanisation
        /// (`humanize` faux : notes exactes, pour une comparaison déterministe).
        public static float[] Render(int sampleRate, int seed = 7, Action<double, float, VoiceKind, double> onTrigger = null, bool humanize = true)
        {
            var engine = new SynthEngine(sampleRate) { OnTrigger = onTrigger };
            double now = 0;
            var board = new SoundBoard(engine, () => now, seed) { Humanize = humanize };
            var pcm = new List<float>((int)(Duration * sampleRate) + 1);
            long rendered = 0;
            void AdvanceTo(double t)
            {
                long target = (long)Math.Round(t * sampleRate);
                if (target > rendered)
                {
                    var chunk = new float[target - rendered];
                    engine.Render(chunk, 1, chunk.Length);
                    pcm.AddRange(chunk);
                    rendered = target;
                }
                now = t;
            }
            for (int g = 0; g < Games.Length; g++)
            {
                board.SetPalette(GamePalettes[g]);
                foreach (var ev in Games[g].Split(';'))
                {
                    var p = ev.Split(' ');
                    AdvanceTo(D(p[0]));
                    switch (p[1])
                    {
                        case "s": board.PlaySelect(int.Parse(p[2], CultureInfo.InvariantCulture)); break;
                        case "c": board.PlayConnect(int.Parse(p[2], CultureInfo.InvariantCulture), D(p[3])); break;
                        case "r": board.PlayRejected(); break;
                        case "k": board.PlayCombo(int.Parse(p[2], CultureInfo.InvariantCulture)); break;
                        case "w": board.PlayWin(); break;
                        case "l": board.PlayLose(); break;
                    }
                }
            }
            AdvanceTo(Duration);
            return pcm.ToArray();
        }

        static double D(string s) => double.Parse(s, CultureInfo.InvariantCulture);
    }
}
