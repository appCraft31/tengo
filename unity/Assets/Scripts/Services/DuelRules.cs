using System;
using TenGo.Core;

namespace TenGo.Services
{
    /// Règles communes des duels (DuelService.swift), indépendantes du serveur.
    public static class DuelRules
    {
        /// Alphabet sans caractères confondables (ni O/0, ni I/1).
        public const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        public const int CodeLength = 6;
        public const int MaxNameLength = 40;

        public static string NewCode(Random rng)
        {
            var c = new char[CodeLength];
            for (int i = 0; i < CodeLength; i++) c[i] = Alphabet[rng.Next(Alphabet.Length)];
            return new string(c);
        }

        /// `code.uppercased().trimmingCharacters(in: .whitespacesAndNewlines)`.
        public static string Normalize(string code) => (code ?? "").Trim().ToUpperInvariant();

        /// `DuelService.displayName` : nom Game Center / Play Games rogné à 40
        /// caractères, sinon « Joueur anonyme » (jamais d'identité inventée).
        public static string DisplayName(string platformName)
        {
            string trimmed = (platformName ?? "").Trim();
            if (trimmed.Length == 0) return Loc.T("duel.anonymous_player");
            return trimmed.Length <= MaxNameLength ? trimmed : trimmed.Substring(0, MaxNameLength);
        }

        /// Refus d'un duel qu'on voudrait rejoindre, dans l'ordre de
        /// `submitOpponentScore` : expiré, déjà joué, son propre duel. Rend la
        /// clé de texte du refus, ou `null` s'il est jouable.
        public static string JoinRefusal(Duel duel, string uid, DateTimeOffset now)
        {
            if (duel.IsExpired(now)) return "duel.error_expired";
            if (duel.OpponentScore != null) return "duel.error_already_played";
            if (uid != null && duel.ChallengerUid == uid) return "duel.error_own_duel";
            return null;
        }
    }
}
