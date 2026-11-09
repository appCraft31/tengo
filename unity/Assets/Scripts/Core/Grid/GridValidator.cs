#nullable enable
using System;
using System.Collections.Generic;

namespace TenGo.Core
{
    /// Validation du défi du jour, portage de GridValidator.swift. Le contrat
    /// n'est pas « entièrement vidable » (recherche sur 2^63 états) mais « offre
    /// une partie riche et non triviale », mesuré en JOUANT la grille au hasard
    /// plusieurs fois, avec les vraies règles.
    public static class GridValidator
    {
        /// En dessous, la grille peut se bloquer trop tôt pour être publiée.
        public const int MinWorstMoves = 4;
        /// En dessous, pas assez de matière jouable.
        public const int MinShortGroups = 6;

        private const int SimulationMaxLen = 5;

        /// Joue `playouts` parties au hasard et en tire des métriques. À
        /// générateur égal, l'évaluation est reproductible. La grille d'origine
        /// n'est pas modifiée.
        public static GridAssessment Assess(GridModel grid, IRng rng, int playouts = 3)
        {
            int easyPairs = grid.CountSumTenGroups(2);
            int shortGroups = grid.CountSumTenGroups(4);
            bool hasMove = grid.HasValidMove();

            var moveCounts = new List<int>();
            var scores = new List<int>();
            for (int i = 0; i < Math.Max(1, playouts); i++)
            {
                Simulate(grid, rng, out int moves, out int score);
                moveCounts.Add(moves);
                scores.Add(score);
            }
            moveCounts.Sort();
            scores.Sort();

            int median = moveCounts[moveCounts.Count / 2];
            int worst = moveCounts[0];
            int medianScore = scores[scores.Count / 2];

            return new GridAssessment(hasMove, easyPairs, shortGroups, median, worst, medianScore,
                DifficultyScore(easyPairs, median, medianScore));
        }

        private static void Simulate(GridModel grid, IRng rng, out int moves, out int score)
        {
            GridModel board = grid.Copy();
            moves = 0;
            score = 0;
            while (true)
            {
                List<List<Cell>> groups = board.SumTenGroups(SimulationMaxLen);
                if (!groups.TryRandomElementSwift(rng, out List<Cell> choice)) break;
                score += Scoring.ForPath(choice.Count);
                board.RemoveBubbles(choice);
                board.ThawFrozenBubbles(choice);
                board.ApplyGravity();
                moves += 1;
                if (moves > 200) break;   // garde-fou, jamais atteint en pratique
            }
        }

        /// Difficulté 1–100. Bornes = centiles 5 et 95 mesurés sur 400 grilles
        /// candidates par l'outil iOS `grid_audit` ; les trois axes sont inversés
        /// (moins de paires, de coups, de score = plus dur).
        public static int DifficultyScore(int easyPairs, int medianMoves, int medianScore)
        {
            double Normalized(int value, int easy, int hard)
            {
                double lo = Math.Min(easy, hard), hi = Math.Max(easy, hard);
                double clamped = Math.Min(Math.Max(value, lo), hi);
                return Math.Abs(clamped - easy) / Math.Abs((double)hard - easy);
            }

            double pairsPart = Normalized(easyPairs, 27, 12);
            double movesPart = Normalized(medianMoves, 17, 10);
            double scorePart = Normalized(medianScore, 620, 230);

            double raw = 0.45 * pairsPart + 0.35 * movesPart + 0.20 * scorePart;
            return Math.Min(100, Math.Max(1, (int)Math.Round(raw * 100, MidpointRounding.AwayFromZero)));
        }
    }
}
