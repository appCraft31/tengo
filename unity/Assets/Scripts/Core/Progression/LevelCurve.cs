#nullable enable
using System;

namespace TenGo.Core
{
    /// Courbe d'expérience et barème des gains (LevelManager.swift).
    public static class LevelCurve
    {
        /// Au-delà, l'XP s'accumule mais le niveau affiché ne bouge plus.
        public const int MaxLevel = 100;

        public const int XpGameCompleted = 10;
        public const int XpChainTier5 = 5;
        public const int XpChainTier10 = 15;
        /// 1 XP par chaîne validée, plafonné (une vingtaine de coups par partie).
        public const int XpPerChain = 1;
        public const int XpChainsCap = 25;
        public const int XpPerfectBoard = 50;
        public const int XpDailyChallenge = 30;
        public const int XpDailyChallengePerfect = 75;
        public const int XpMission = 15;
        public const int XpSuperMission = 40;
        public const int XpRush = 20;
        public const int XpPuzzleSolved = 40;
        /// Puzzle abandonné : consolation (= `xpGameCompleted` sur iOS).
        public const int XpPuzzleAttempt = XpGameCompleted;
        public const int XpDuel = 25;

        /// XP pour passer du niveau `level` au suivant : croissance linéaire.
        public static int XpRequired(int level) => 60 + (level - 1) * 12;

        public static LevelBreakdown Breakdown(int totalXP)
        {
            int level = 1;
            int remaining = Math.Max(0, totalXP);
            while (level < MaxLevel && remaining >= XpRequired(level))
            {
                remaining -= XpRequired(level);
                level++;
            }
            return new LevelBreakdown(level, remaining, level >= MaxLevel ? 0 : XpRequired(level));
        }

        public static int LevelFor(int totalXP) => Breakdown(totalXP).Level;

        private static readonly (int level, string key)[] Titles =
        {
            (1, "level.title.beginner"),
            (5, "level.title.learner"),
            (10, "level.title.thinker"),
            (20, "level.title.strategist"),
            (30, "level.title.expert"),
            (50, "level.title.master"),
            (75, "level.title.zen_master"),
            (100, "level.title.ten_master"),
        };

        /// Clé de texte du titre : plus haut palier atteint par `level`.
        public static string TitleKey(int level)
        {
            string key = Titles[0].key;
            foreach (var (threshold, k) in Titles)
                if (threshold <= level) key = k;
            return key;
        }

        /// XP de fin de partie normale.
        public static int XpForGame(int longestChain, int chainsCommitted, bool isPerfect) =>
            XpGameCompleted
            + (longestChain >= 10 ? XpChainTier10 : longestChain >= 5 ? XpChainTier5 : 0)
            + Math.Min(XpChainsCap, Math.Max(0, chainsCommitted) * XpPerChain)
            + (isPerfect ? XpPerfectBoard : 0);
    }
}
