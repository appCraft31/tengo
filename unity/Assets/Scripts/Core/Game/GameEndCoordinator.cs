#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace TenGo.Core
{
    /// Effets de partie sur la progression : portage de `GameScene.recordScore()`
    /// et des rapports en cours de partie (série, missions, stats, succès,
    /// miroir, graine), d'après la version Android (GameEndCoordinator.kt).
    public sealed class GameEndCoordinator
    {
        private readonly GameStateRepository _games;
        private readonly EconomyRepository _economy;
        private readonly LevelRepository _level;
        private readonly MissionRepository _missions;
        private readonly PlayerStatsRepository _stats;
        private readonly AchievementRepository _achievements;
        private readonly PuzzleProgressRepository _puzzles;
        private readonly StreakRepository _streak;
        private readonly IClock _clock;
        private readonly IGameAnalytics? _analytics;
        private readonly MirrorRepository? _mirror;
        private readonly SeedRepository? _seeds;
        private readonly FirstRunState? _firstRun;

        public GameEndCoordinator(GameStateRepository games, EconomyRepository economy, LevelRepository level,
            MissionRepository missions, PlayerStatsRepository stats, AchievementRepository achievements,
            PuzzleProgressRepository puzzles, StreakRepository streak, IClock clock, IGameAnalytics? analytics = null,
            MirrorRepository? mirror = null, SeedRepository? seeds = null, FirstRunState? firstRun = null)
        {
            _games = games;
            _economy = economy;
            _level = level;
            _missions = missions;
            _stats = stats;
            _achievements = achievements;
            _puzzles = puzzles;
            _streak = streak;
            _clock = clock;
            _analytics = analytics;
            _mirror = mirror;
            _seeds = seeds;
            _firstRun = firstRun;
        }

        /// Une graine vient d'être plantée : programme l'annonce d'éclosion
        /// (notification locale), date en ms Unix.
        public event Action<long>? SeedPlanted;

        /// Début de partie (jamais en reprise, jamais en démo) : missions du
        /// jour, série, paliers de série.
        public GameStartInfo OnGameStarted(GameMode mode)
        {
            if (mode.Kind == GameModeKind.Demo) return GameStartInfo.Nothing;
            _missions.EnsureRolled(DayKeys.Challenge(_clock));
            bool protectedByShield = _streak.RegisterPlay();
            List<StreakMilestone> paid = _streak.AwardMilestones(_streak.Current);
            _firstRun?.GameStarted(mode.AnalyticsName);
            return new GameStartInfo(protectedByShield, paid);
        }

        /// Chaîne validée (hors démo) : missions, stats, succès débloqués en direct.
        public List<AchievementDefinition> OnChainCommitted(GameMode mode, int length, int points)
        {
            if (mode.Kind == GameModeKind.Demo) return new List<AchievementDefinition>();
            _missions.ReportMove();
            _missions.ReportChain(length);
            _stats.ReportChain(length);
            _missions.ReportScore(points);
            return _achievements.CheckForNewUnlocks();
        }

        /// Bonus (+1000) crédité hors chaîne : victoire, ou grille vidée en Rush.
        public void OnBonusScored(GameMode mode, int points)
        {
            if (mode.Kind == GameModeKind.Demo) return;
            _missions.ReportScore(points);
        }

        /// Grille vidée en Rush : ce n'est pas une fin de partie.
        public List<AchievementDefinition> OnRushBoardCleared()
        {
            _missions.ReportPerfectBoard();
            _stats.ReportPerfectBoard();
            return _achievements.CheckForNewUnlocks();
        }

        public GameEndResult OnGameEnded(GameEndInput input)
        {
            if (input.ContinuedFrom != null) return OnContinuationEnded(input, input.ContinuedFrom);
            GameMode mode = input.Mode;
            int score = input.Score;
            bool won = input.Won;
            bool isDemo = mode.Kind == GameModeKind.Demo;
            int coins = 0;
            GainResult? xp = null;
            int stars = 0;

            // Statut du record, lu AVANT l'insertion du score.
            RecordStatus record;
            switch (mode.Kind)
            {
                case GameModeKind.Normal:
                case GameModeKind.Rush:
                    IReadOnlyList<int> board = mode.Kind == GameModeKind.Rush ? _games.RushHighScores : _games.HighScores;
                    record = RecordStatus.Of(score, board.Count > 0 ? board[0] : (int?)null);
                    break;
                case GameModeKind.Daily:
                    record = _games.HighScores.Count > 0 ? RecordStatus.Best(_games.HighScores[0]) : RecordStatus.None;
                    break;
                default:
                    record = RecordStatus.None;
                    break;
            }

            switch (mode.Kind)
            {
                case GameModeKind.Normal:
                    _games.AddScore(score);
                    _games.Clear();
                    coins = _economy.AwardForScore(score);
                    xp = _level.AwardForGame(input.LongestChain, input.Combos, won);
                    _missions.ReportGameEnded(won);
                    _stats.ReportGameEnded(mode.Kind, won);
                    break;
                case GameModeKind.Daily:
                    // Pièces et XP une seule fois, à la première fin de partie du jour.
                    if (!_economy.IsDailyCompleted(input.DailyDayKey))
                    {
                        _economy.AwardDailyChallenge();
                        xp = _level.AwardForDailyChallenge(won);
                        _missions.ReportGameEnded(won);
                        _stats.ReportGameEnded(mode.Kind, won);
                    }
                    _economy.MarkDailyCompleted(input.DailyDayKey);
                    _analytics?.DailyChallengePlayed(score);
                    break;
                case GameModeKind.Rush:
                    _games.AddRushScore(score);
                    xp = _level.AwardForRush();
                    _missions.ReportGameEnded(false);
                    _stats.ReportGameEnded(mode.Kind, false);
                    break;
                case GameModeKind.Puzzle:
                    if (input.Puzzle != null && won)
                    {
                        stars = input.Puzzle.Stars(score);
                        _puzzles.Record(input.Puzzle.World, input.Puzzle.Index, stars);
                    }
                    xp = _level.AwardForPuzzle(won);
                    _missions.ReportGameEnded(won);
                    _stats.ReportGameEnded(mode.Kind, won);
                    break;
                case GameModeKind.Duel:
                    xp = _level.AwardForDuel();
                    _missions.ReportGameEnded(won);
                    _stats.ReportGameEnded(mode.Kind, won);
                    break;
                case GameModeKind.Demo:
                    // Rien n'est enregistré ; seules les étoiles d'un puzzle sont
                    // calculées, pour que la capture montre la vraie récompense.
                    if (input.Puzzle != null && won) stars = input.Puzzle.Stars(score);
                    break;
            }
            if (!isDemo) _analytics?.LevelEnd(mode.AnalyticsName, score, won);

            // Miroir cognitif, graine de dix, première partie (parité `recordScore`).
            MirrorInsight? insight = null;
            SeedOutcome? seedOutcome = null;
            int seedPower = 0;
            bool firstGame = false;
            if (!isDemo)
            {
                if (input.MirrorTally != null && _mirror != null)
                {
                    insight = _mirror.FinishGame(input.MirrorTally);
                    if (insight is MirrorInsight shown) _analytics?.MirrorInsightShown(shown.AnalyticsName);
                }
                OfferSeed(input.LongestChain, ref seedOutcome, ref seedPower);
            }
            List<AchievementDefinition> fresh = isDemo ? new List<AchievementDefinition>() : _achievements.CheckForNewUnlocks();
            if (!isDemo && _firstRun != null) firstGame = _firstRun.GameCompleted(mode.AnalyticsName, score, won);

            return new GameEndResult(mode, score, won, input.Combos, input.LongestChain, coins, xp, record, stars, fresh,
                Suggest(mode), _stats.TotalChainsMade, insight, seedOutcome, seedPower, firstGame);
        }

        /// Étape suivante proposée : défi du jour s'il reste à faire, sinon Rush.
        private NextSuggestion Suggest(GameMode mode)
        {
            GameModeKind k = mode.Kind;
            if (k != GameModeKind.Normal && k != GameModeKind.Daily && k != GameModeKind.Rush) return NextSuggestion.None;
            if (k != GameModeKind.Daily && !_economy.IsDailyCompleted(DayKeys.Challenge(_clock))) return NextSuggestion.NextDaily;
            if (k != GameModeKind.Rush) return NextSuggestion.NextRush;
            return NextSuggestion.None;
        }

        private void OfferSeed(int longestChain, ref SeedOutcome? outcome, ref int power)
        {
            if (_seeds == null) return;
            var (seed, result) = _seeds.Offer(longestChain);
            outcome = result;
            power = seed?.Power ?? 0;
            if (result.Kind == SeedOutcomeKind.Planted)
            {
                _analytics?.SeedPlanted(result.Power, false);
                SeedPlanted?.Invoke(result.BloomAtMs);
            }
            else if (result.Kind == SeedOutcomeKind.Strengthened)
            {
                _analytics?.SeedPlanted(result.Power, true);
            }
        }

        /// Fin d'une partie continuée (vidéo « Continuer », mode normal) : déjà
        /// comptée. Seuls les points en plus comptent — record, pièces complétées,
        /// graine si la plus longue chaîne a grandi. Ni XP, ni missions, ni miroir
        /// en double.
        private GameEndResult OnContinuationEnded(GameEndInput input, GameEndResult previous)
        {
            int score = input.Score;
            var others = _games.HighScores.ToList();
            others.Remove(previous.Score);
            RecordStatus record = RecordStatus.Of(score, others.Count > 0 ? others[0] : (int?)null);
            _games.ReplaceScore(previous.Score, score);
            _games.Clear();
            int coins = _economy.TopUpForScore(score, previous.CoinsEarned);
            _analytics?.LevelEnd(input.Mode.AnalyticsName, score, input.Won);

            SeedOutcome? seedOutcome = previous.SeedOutcome;
            int seedPower = previous.SeedPower;
            if (input.LongestChain > previous.LongestChain) OfferSeed(input.LongestChain, ref seedOutcome, ref seedPower);

            return new GameEndResult(previous.Mode, score, input.Won, input.Combos, input.LongestChain, coins, null, record,
                previous.PuzzleStars, _achievements.CheckForNewUnlocks(), previous.Suggestion, _stats.TotalChainsMade,
                previous.MirrorInsight, seedOutcome, seedPower, previous.WasFirstGame);
        }
    }
}
