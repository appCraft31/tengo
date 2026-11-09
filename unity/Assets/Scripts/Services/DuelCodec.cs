using System;
using System.Collections.Generic;

namespace TenGo.Services
{
    /// Format Firestore des duels (`duels/{code}`), au champ près celui de
    /// DuelService.swift et FirestoreDuelService.kt, pour qu'un duel lancé sur
    /// une app native se joue sur Unity et inversement :
    ///
    /// | Champ | Type Firestore | Valeur |
    /// |---|---|---|
    /// | `seed` | entier 64 bits | graine `UInt64` lue en `Int64` (mêmes bits) |
    /// | `challengerUid`, `challengerName` | chaîne | uid anonyme, nom ≤ 40 |
    /// | `challengerScore` | entier | score du challenger |
    /// | `opponentUid` | null puis chaîne | null à la création (exigé par les règles) |
    /// | `opponentName`, `opponentScore` | chaîne, entier | absents jusqu'à la réponse |
    /// | `expiresAt` | horodatage | création + 7 jours |
    /// | `createdAt` | horodatage serveur | `FieldValue.serverTimestamp()` |
    ///
    /// Aucun type Firebase ici (testable en EditMode) : les dates sont des
    /// `DateTimeOffset` et l'horodatage serveur le jeton `ServerTimestamp`,
    /// que `FirestoreDuelBackend` convertit.
    public static class DuelCodec
    {
        /// Jeton remplacé par `FieldValue.ServerTimestamp` à l'écriture.
        public static readonly object ServerTimestamp = new ServerTimestampToken();
        sealed class ServerTimestampToken { public override string ToString() => "serverTimestamp()"; }

        public const string Collection = "duels";

        public static Dictionary<string, object> CreateFields(long seed, string uid, string name, int score, DateTimeOffset expiresAt) => new()
        {
            ["seed"] = seed,
            ["challengerUid"] = uid,
            ["challengerName"] = name,
            ["challengerScore"] = (long)score,
            ["opponentUid"] = null,
            ["expiresAt"] = expiresAt,
            ["createdAt"] = ServerTimestamp,
        };

        public static Dictionary<string, object> OpponentFields(string uid, string name, int score) => new()
        {
            ["opponentUid"] = uid,
            ["opponentName"] = name,
            ["opponentScore"] = (long)score,
        };

        /// `DuelService.decode` : `null` si un champ obligatoire manque ou n'a
        /// pas le bon type (graine, uid et score du challenger, échéance).
        public static Duel Decode(string code, IDictionary<string, object> data)
        {
            if (data == null) return null;
            if (!TryInt(data, "seed", out long seed)) return null;
            if (!(Get(data, "challengerUid") is string challengerUid)) return null;
            if (!TryInt(data, "challengerScore", out long challengerScore)) return null;
            if (!(Get(data, "expiresAt") is DateTimeOffset expiresAt)) return null;
            return new Duel
            {
                Code = code,
                Seed = seed,
                ChallengerUid = challengerUid,
                ChallengerName = Get(data, "challengerName") as string ?? "",
                ChallengerScore = (int)challengerScore,
                OpponentUid = Get(data, "opponentUid") as string,
                OpponentName = Get(data, "opponentName") as string,
                OpponentScore = TryInt(data, "opponentScore", out long opp) ? (int)opp : null,
                ExpiresAt = expiresAt,
            };
        }

        static object Get(IDictionary<string, object> d, string key) => d.TryGetValue(key, out var v) ? v : null;

        static bool TryInt(IDictionary<string, object> d, string key, out long value)
        {
            switch (Get(d, key))
            {
                case long l: value = l; return true;
                case int i: value = i; return true;
                default: value = 0; return false;
            }
        }
    }
}
