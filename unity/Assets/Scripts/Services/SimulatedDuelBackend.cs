using System;
using System.Collections.Generic;
using TenGo.Core;
using TenGo.Fx;

namespace TenGo.Services
{
    /// Duels simulés en local (éditeur, tests) : codes au format iOS, réponse
    /// après un court délai réel, historique local (`DuelHistoryRepository`),
    /// mêmes refus que le serveur. Un code inconnu bien formé rejoint un duel
    /// fictif dont le challenger a marqué `SimulatedRivalScore`, sur une graine
    /// tirée du code.
    public sealed class SimulatedDuelBackend : IDuelBackend
    {
        readonly Dictionary<string, Duel> _duels = new();
        readonly DuelHistoryRepository _history;
        readonly Random _rng;
        readonly Func<DateTimeOffset> _now;

        public float Latency = 0.4f;
        public int SimulatedRivalScore = 1200;
        public bool Fail;
        /// Faux : les codes inconnus sont introuvables (au lieu d'un duel fictif).
        public bool InventsUnknownDuels = true;

        public SimulatedDuelBackend(DuelHistoryRepository history, int? seed = null, Func<DateTimeOffset> now = null)
        {
            _history = history;
            _rng = seed.HasValue ? new Random(seed.Value) : new Random();
            _now = now ?? (() => DateTimeOffset.UtcNow);
        }

        public string Uid => "local-player";

        /// Ajoute un duel connu du « serveur » (tests).
        public void Seed(Duel duel) => _duels[duel.Code] = duel;

        void Later(Action a) => FxTime.AfterRealtime(Latency, a);

        public void CreateDuel(long seed, int score, Action<Duel> onDone, Action<string> onError)
        {
            Later(() =>
            {
                if (Fail) { onError?.Invoke(Loc.T("duel.error_not_found")); return; }
                var duel = new Duel
                {
                    Code = DuelRules.NewCode(_rng), Seed = seed, ChallengerUid = Uid, ChallengerName = DuelRules.DisplayName(null),
                    ChallengerScore = score, ExpiresAt = _now() + Duel.Lifetime,
                };
                _duels[duel.Code] = duel;
                _history?.Remember(duel.Code);
                onDone?.Invoke(duel);
            });
        }

        Duel Find(string code)
        {
            if (_duels.TryGetValue(code, out var duel)) return duel;
            if (!InventsUnknownDuels || code.Length != DuelRules.CodeLength) return null;
            duel = new Duel
            {
                Code = code, Seed = SeedForCode(code), ChallengerUid = "simulated-rival", ChallengerName = "Rival",
                ChallengerScore = SimulatedRivalScore, ExpiresAt = _now() + Duel.Lifetime,
            };
            _duels[code] = duel;
            return duel;
        }

        public void Fetch(string code, Action<Duel> onDone, Action<string> onError)
        {
            string normalized = DuelRules.Normalize(code);
            Later(() =>
            {
                var duel = Fail ? null : Find(normalized);
                if (duel == null) onError?.Invoke(Loc.T("duel.error_not_found"));
                else onDone?.Invoke(duel);
            });
        }

        public void SubmitOpponentScore(string code, int score, Action<Duel> onDone, Action<string> onError)
        {
            string normalized = DuelRules.Normalize(code);
            Later(() =>
            {
                var duel = Fail ? null : Find(normalized);
                if (duel == null) { onError?.Invoke(Loc.T("duel.error_not_found")); return; }
                string refusal = DuelRules.JoinRefusal(duel, Uid, _now());
                if (refusal != null) { onError?.Invoke(Loc.T(refusal)); return; }
                duel.OpponentUid = Uid;
                duel.OpponentName = DuelRules.DisplayName(null);
                duel.OpponentScore = score;
                _history?.Remember(duel.Code);
                onDone?.Invoke(duel);
            });
        }

        public void MyDuels(Action<IReadOnlyList<Duel>> onDone)
        {
            Later(() =>
            {
                var list = new List<Duel>();
                if (_history != null)
                    foreach (var code in _history.Codes)
                        if (_duels.TryGetValue(code, out var d)) list.Add(d);
                onDone?.Invoke(list);
            });
        }

        /// Graine stable tirée du code (FNV-1a 64 bits), pour les duels fictifs.
        public static long SeedForCode(string code)
        {
            ulong h = 14695981039346656037UL;
            foreach (char c in code)
            {
                h ^= c;
                h *= 1099511628211UL;
            }
            if (h == 0) h = 1;
            return unchecked((long)h);
        }
    }
}
