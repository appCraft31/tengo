using System;

namespace TenGo.Services
{
    /// Issue d'un duel du point de vue d'un joueur.
    public enum DuelOutcome { Win, Loss, Draw }

    /// Duel (DuelService.swift `Duel`) : une graine partagée, deux scores.
    public sealed class Duel
    {
        /// Durée de vie d'un duel : au-delà, il n'est plus jouable (`DuelService.lifetime`).
        public static readonly TimeSpan Lifetime = TimeSpan.FromDays(7);

        public string Code;
        public long Seed;
        public string ChallengerUid;
        public string ChallengerName;
        public int ChallengerScore;
        public string OpponentUid;
        public string OpponentName;
        public int? OpponentScore;
        public DateTimeOffset ExpiresAt = DateTimeOffset.MaxValue;

        public bool IsComplete => OpponentScore != null;
        public bool IsExpired(DateTimeOffset now) => now > ExpiresAt;

        /// Issue pour `uid`, une fois les deux scores connus (`null` sinon).
        public DuelOutcome? OutcomeFor(string uid)
        {
            if (OpponentScore is not int opp || OpponentUid == null) return null;
            int mine, theirs;
            if (uid == ChallengerUid) { mine = ChallengerScore; theirs = opp; }
            else if (uid == OpponentUid) { mine = opp; theirs = ChallengerScore; }
            else return null;
            if (mine > theirs) return DuelOutcome.Win;
            if (mine < theirs) return DuelOutcome.Loss;
            return DuelOutcome.Draw;
        }

        /// Nom de l'adversaire vu par `uid`.
        public string RivalName(string uid) => uid == ChallengerUid ? (OpponentName ?? "") : (ChallengerName ?? "");
    }
}
