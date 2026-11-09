using System;
using System.Collections.Generic;
using TenGo.Core;
using TenGo.Fx;

namespace TenGo.Services
{
    /// Duels simulés en local (en attendant Firestore, étape 7) : codes au
    /// format iOS (6 caractères sans 0/O/1/I), réponse après un court délai
    /// réel, historique local (`DuelHistoryRepository`, clé iOS). Un code
    /// inconnu rejoint un duel fictif dont le challenger a marqué
    /// `SimulatedRivalScore`.
    public sealed class SimulatedDuelBackend : IDuelBackend
    {
        const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

        readonly Dictionary<string, Duel> _duels = new();
        readonly DuelHistoryRepository _history;
        readonly Random _rng;

        public float Latency = 0.4f;
        public int SimulatedRivalScore = 1200;
        public bool Fail;

        public SimulatedDuelBackend(DuelHistoryRepository history, int? seed = null)
        {
            _history = history;
            _rng = seed.HasValue ? new Random(seed.Value) : new Random();
        }

        public string Uid => "local-player";

        string NewCode()
        {
            var c = new char[6];
            for (int i = 0; i < 6; i++) c[i] = Alphabet[_rng.Next(Alphabet.Length)];
            return new string(c);
        }

        public void CreateDuel(long seed, int score, Action<Duel> onDone, Action<string> onError)
        {
            FxTime.AfterRealtime(Latency, () =>
            {
                if (Fail) { onError?.Invoke(Loc.T("duel.error_not_found")); return; }
                var duel = new Duel { Code = NewCode(), Seed = seed, ChallengerUid = Uid, ChallengerName = "Moi", ChallengerScore = score };
                _duels[duel.Code] = duel;
                _history?.Remember(duel.Code);
                onDone?.Invoke(duel);
            });
        }

        public void SubmitOpponentScore(string code, int score, Action<Duel> onDone, Action<string> onError)
        {
            FxTime.AfterRealtime(Latency, () =>
            {
                if (Fail) { onError?.Invoke(Loc.T("duel.error_not_found")); return; }
                if (!_duels.TryGetValue(code, out var duel))
                {
                    duel = new Duel { Code = code, ChallengerUid = "simulated-rival", ChallengerName = "Rival", ChallengerScore = SimulatedRivalScore };
                    _duels[code] = duel;
                }
                duel.OpponentUid = Uid;
                duel.OpponentName = "Moi";
                duel.OpponentScore = score;
                _history?.Remember(code);
                onDone?.Invoke(duel);
            });
        }
    }
}
