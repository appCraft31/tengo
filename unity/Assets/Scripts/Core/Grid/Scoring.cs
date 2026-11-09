#nullable enable
using System;

namespace TenGo.Core
{
    /// Barème du score, portage de ScoreRules.swift (source unique).
    ///
    /// Une chaîne longue doit valoir bien plus que la somme des courtes qu'elle
    /// remplace : chaque coup retire exactement 10 points de valeur du plateau,
    /// donc vider une grille demande toujours le même nombre de coups ; sans
    /// récompense superlinéaire, jouer fin ne rapporterait rien de plus.
    public static class Scoring
    {
        /// Points d'une chaîne de `length` bulles. Jusqu'à 4, inchangé depuis la
        /// sortie du jeu ; au-delà, progression quadratique.
        ///
        ///     2 → 10      5 → 200     8 → 800
        ///     3 → 30      6 → 350     9 → 1100
        ///     4 → 100     7 → 550    10 → 1450
        public static int ForPath(int length)
        {
            if (length < 2) return 0;
            if (length == 2) return 10;
            if (length == 3) return 30;
            int k = length - 4;
            return 100 + 100 * k + 25 * k * (k - 1);
        }

        /// Multiplicateur affiché au joueur, arrondi au dixième (arrondi Swift
        /// `.rounded()` : demi vers l'extérieur, pas l'arrondi bancaire de .NET).
        public static double DisplayMultiplier(int length)
        {
            if (length < 2) return 0;
            double flat = length * 10;
            return Math.Round(ForPath(length) / flat * 10, MidpointRounding.AwayFromZero) / 10;
        }

        /// Pièces de fin de partie normale : min(10, 5 + score/120) (CoinManager.swift).
        public const int ScoreCoinBase = 5;
        public const int ScoreCoinDivisor = 120;
        public const int ScoreCoinMax = 10;

        public static int CoinsForScore(int score) =>
            Math.Min(ScoreCoinMax, ScoreCoinBase + Math.Max(0, score) / ScoreCoinDivisor);
    }
}
