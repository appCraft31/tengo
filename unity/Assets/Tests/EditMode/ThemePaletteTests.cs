using System;
using System.Linq;
using NUnit.Framework;
using TenGo.Core;

namespace TenGo.Tests
{
    /// Palettes des thèmes payants recalculées pour la refonte 4.0 : chiffre
    /// lisible sur chaque bulle (WCAG ≥ 4,5:1) et neuf bulles nettement
    /// distinctes, en vision normale comme sous les trois daltonismes
    /// (simulation de Machado 2009, sévérité 1). Écarts en ΔE OKLab × 100.
    public class ThemePaletteTests
    {
        static double Lin(double c) => c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        static double Lum(RgbColor c) => 0.2126 * Lin(c.R) + 0.7152 * Lin(c.G) + 0.0722 * Lin(c.B);

        static double Contrast(RgbColor a, RgbColor b)
        {
            double la = Lum(a), lb = Lum(b);
            return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
        }

        static readonly double[][] Deutan = { new[] { 0.367322, 0.860646, -0.227968 }, new[] { 0.280085, 0.672501, 0.047413 }, new[] { -0.011820, 0.042940, 0.968881 } };
        static readonly double[][] Protan = { new[] { 0.152286, 1.052583, -0.204868 }, new[] { 0.114503, 0.786281, 0.099216 }, new[] { -0.003882, -0.048116, 1.051998 } };
        static readonly double[][] Tritan = { new[] { 1.255528, -0.076749, -0.178779 }, new[] { -0.078411, 0.930809, 0.147602 }, new[] { 0.004733, 0.691367, 0.303900 } };

        /// Couleur → OKLab, après simulation éventuelle (en RVB linéaire).
        static double[] OkLab(RgbColor c, double[][] sim)
        {
            double r = Lin(c.R), g = Lin(c.G), b = Lin(c.B);
            if (sim != null)
            {
                double r2 = sim[0][0] * r + sim[0][1] * g + sim[0][2] * b;
                double g2 = sim[1][0] * r + sim[1][1] * g + sim[1][2] * b;
                double b2 = sim[2][0] * r + sim[2][1] * g + sim[2][2] * b;
                r = Math.Clamp(r2, 0, 1); g = Math.Clamp(g2, 0, 1); b = Math.Clamp(b2, 0, 1);
            }
            double l = Math.Cbrt(0.4122214708 * r + 0.5363325363 * g + 0.0514459929 * b);
            double m = Math.Cbrt(0.2119034982 * r + 0.6806995451 * g + 0.1073969566 * b);
            double s = Math.Cbrt(0.0883024619 * r + 0.2817188376 * g + 0.6299787005 * b);
            return new[]
            {
                0.2104542553 * l + 0.7936177850 * m - 0.0040720468 * s,
                1.9779984951 * l - 2.4285922050 * m + 0.4505937099 * s,
                0.0259040371 * l + 0.7827717662 * m - 0.8086757660 * s,
            };
        }

        static double WorstDeltaE(Theme t, double[][] sim)
        {
            var labs = t.Bubbles.Select(c => OkLab(c, sim)).ToArray();
            double worst = double.MaxValue;
            for (int i = 0; i < labs.Length; i++)
                for (int j = i + 1; j < labs.Length; j++)
                {
                    double d = Math.Sqrt(Math.Pow(labs[i][0] - labs[j][0], 2) + Math.Pow(labs[i][1] - labs[j][1], 2) + Math.Pow(labs[i][2] - labs[j][2], 2));
                    worst = Math.Min(worst, d * 100);
                }
            return worst;
        }

        static readonly string[] Paid = { "forest", "ocean", "desert", "candy", "space", "night" };

        [Test]
        public void DigitIsReadableOnEveryBubble([ValueSource(nameof(Paid))] string id)
        {
            var t = ThemeCatalog.ById(id);
            foreach (var c in t.Bubbles)
                Assert.GreaterOrEqual(Contrast(t.Digit, c), 4.5, $"{id} : chiffre sur {c}");
            // L'accent est une des bulles : le chiffre sert d'encre sur les boutons d'accent.
            Assert.IsTrue(t.Bubbles.Contains(t.Accent), id);
        }

        [Test]
        public void BubblesStayDistinct_AlsoForColorBlindness([ValueSource(nameof(Paid))] string id)
        {
            var t = ThemeCatalog.ById(id);
            Assert.GreaterOrEqual(WorstDeltaE(t, null), 7.0, $"{id} normal");
            Assert.GreaterOrEqual(WorstDeltaE(t, Deutan), 5.0, $"{id} deutéranopie");
            Assert.GreaterOrEqual(WorstDeltaE(t, Protan), 5.0, $"{id} protanopie");
            Assert.GreaterOrEqual(WorstDeltaE(t, Tritan), 5.0, $"{id} tritanopie");
        }
    }
}
