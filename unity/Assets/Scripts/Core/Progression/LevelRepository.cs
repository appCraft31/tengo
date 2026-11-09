#nullable enable
namespace TenGo.Core
{
    /// XP et niveaux (LevelManager.swift). L'XP ne décroît jamais.
    public sealed class LevelRepository
    {
        private readonly ISaveStore _store;

        public LevelRepository(ISaveStore store)
        {
            _store = store;
        }

        public int TotalXP => _store.GetInt(SaveKeys.TotalXP);

        public LevelBreakdown Breakdown => LevelCurve.Breakdown(TotalXP);

        public int Level => Breakdown.Level;

        /// Ajoute de l'XP ; un montant nul ou négatif n'écrit rien.
        public GainResult Award(int xp)
        {
            int before = LevelCurve.LevelFor(TotalXP);
            if (xp <= 0) return new GainResult(0, before, before);
            _store.SetInt(SaveKeys.TotalXP, TotalXP + xp);
            return new GainResult(xp, before, LevelCurve.LevelFor(TotalXP));
        }

        public GainResult AwardForGame(int longestChain, int chainsCommitted, bool isPerfect) =>
            Award(LevelCurve.XpForGame(longestChain, chainsCommitted, isPerfect));

        /// Une seule fois par jour (même garde que la récompense en pièces).
        public GainResult AwardForDailyChallenge(bool isPerfect) =>
            Award(isPerfect ? LevelCurve.XpDailyChallengePerfect : LevelCurve.XpDailyChallenge);

        public GainResult AwardForMission(bool isSuper) =>
            Award(isSuper ? LevelCurve.XpSuperMission : LevelCurve.XpMission);

        public GainResult AwardForRush() => Award(LevelCurve.XpRush);

        public GainResult AwardForPuzzle(bool completed) =>
            Award(completed ? LevelCurve.XpPuzzleSolved : LevelCurve.XpPuzzleAttempt);

        public GainResult AwardForDuel() => Award(LevelCurve.XpDuel);

        public GainResult AwardForAchievement(int xp) => Award(xp);
    }
}
