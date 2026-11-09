#nullable enable
using System.Collections.Generic;

namespace TenGo.Core
{
    /// Bilan consolidé de fin de partie, qui alimente l'écran de fin.
    public sealed class GameEndResult
    {
        public GameEndResult(GameMode mode, int score, bool won, int combos, int longestChain, int coinsEarned,
            GainResult? xp, RecordStatus record, int puzzleStars, IReadOnlyList<AchievementDefinition> newAchievements,
            NextSuggestion suggestion, int totalChainsMade, MirrorInsight? mirrorInsight = null,
            SeedOutcome? seedOutcome = null, int seedPower = 0, bool wasFirstGame = false)
        {
            Mode = mode;
            Score = score;
            Won = won;
            Combos = combos;
            LongestChain = longestChain;
            CoinsEarned = coinsEarned;
            Xp = xp;
            Record = record;
            PuzzleStars = puzzleStars;
            NewAchievements = newAchievements;
            Suggestion = suggestion;
            TotalChainsMade = totalChainsMade;
            MirrorInsight = mirrorInsight;
            SeedOutcome = seedOutcome;
            SeedPower = seedPower;
            WasFirstGame = wasFirstGame;
        }

        public GameMode Mode { get; }
        public int Score { get; }
        public bool Won { get; }
        public int Combos { get; }
        public int LongestChain { get; }
        public int CoinsEarned { get; }
        /// XP gagnée (`null` : aucune, ex. défi déjà payé aujourd'hui, démo, continuation).
        public GainResult? Xp { get; }
        public RecordStatus Record { get; }
        public int PuzzleStars { get; }
        public IReadOnlyList<AchievementDefinition> NewAchievements { get; }
        public NextSuggestion Suggestion { get; }
        /// Total des chaînes : sa parité choisit l'encouragement « progress » / « again ».
        public int TotalChainsMade { get; }
        /// Phrase du miroir (`null` : rien à dire).
        public MirrorInsight? MirrorInsight { get; }
        /// Sort de la graine de dix.
        public SeedOutcome? SeedOutcome { get; }
        /// Multiplicateur de la graine en cours après la partie (icône).
        public int SeedPower { get; }
        /// Toute première partie terminée de l'installation (TEN-4).
        public bool WasFirstGame { get; }

        public bool IsNewRecord => Record.Kind == RecordStatusKind.NewRecord;

        public int? NearMissGap => Record.Kind == RecordStatusKind.NearMiss ? Record.Value : (int?)null;
    }
}
