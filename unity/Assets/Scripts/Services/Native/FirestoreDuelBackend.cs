#if TENGO_FIREBASE
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Firebase.Auth;
using Firebase.Extensions;
using Firebase.Firestore;
using TenGo.Core;
using UnityEngine;

namespace TenGo.Services
{
    /// Duels sur Firestore (DuelService.swift + FirebaseService.swift) :
    /// connexion anonyme paresseuse (au premier usage réel du duel), documents
    /// `duels/{code}` au format de `DuelCodec`, refus clairs côté client (les
    /// règles Firestore restent la vraie barrière). Toute erreur rend la main
    /// avec un message ; rien ne bloque le jeu.
    public sealed class FirestoreDuelBackend : IDuelBackend
    {
        readonly DuelHistoryRepository _history;
        readonly Func<string> _playerName;
        readonly System.Random _rng = new();

        public FirestoreDuelBackend(DuelHistoryRepository history, Func<string> playerName)
        {
            _history = history;
            _playerName = playerName;
        }

        public string Uid => FirebaseBootstrap.IsAvailable ? FirebaseAuth.DefaultInstance.CurrentUser?.UserId : null;

        CollectionReference Duels => FirebaseFirestore.DefaultInstance.Collection(DuelCodec.Collection);

        /// `signInIfNeeded` : idempotent, ne recrée jamais de compte.
        void SignIn(Action<string> onUid, Action<string> onError)
        {
            FirebaseBootstrap.WhenReady(ok =>
            {
                if (!ok) { onError("Connexion impossible : Firebase indisponible."); return; }
                var auth = FirebaseAuth.DefaultInstance;
                if (auth.CurrentUser != null) { onUid(auth.CurrentUser.UserId); return; }
                auth.SignInAnonymouslyAsync().ContinueWithOnMainThread(task =>
                {
                    if (task.IsFaulted || task.IsCanceled || task.Result?.User == null) { onError(AuthMessage(task.Exception)); return; }
                    onUid(task.Result.User.UserId);
                });
            });
        }

        /// FirebaseService.mapped : console non configurée, hors ligne, ou autre.
        static string AuthMessage(Exception e)
        {
            string details = string.Join(" | ", Flatten(e).Select(x => x.Message));
            if (details.Contains("CONFIGURATION_NOT_FOUND") || details.Contains("ADMIN_ONLY_OPERATION") || details.Contains("OPERATION_NOT_ALLOWED"))
                return "L'authentification anonyme n'est pas activée sur le projet Firebase.";
            if (Flatten(e).OfType<Firebase.FirebaseException>().Any(f => f.ErrorCode == (int)AuthError.NetworkRequestFailed))
                return "Aucune connexion réseau.";
            return "Connexion impossible : " + details;
        }

        static IEnumerable<Exception> Flatten(Exception e)
        {
            if (e == null) yield break;
            if (e is AggregateException agg)
            {
                foreach (var inner in agg.Flatten().InnerExceptions)
                    foreach (var x in Flatten(inner)) yield return x;
                yield break;
            }
            yield return e;
            foreach (var x in Flatten(e.InnerException)) yield return x;
        }

        static string BackendMessage(Exception e) => Flatten(e).LastOrDefault()?.Message ?? Loc.T("duel.error_not_found");

        static Dictionary<string, object> ToFirestore(Dictionary<string, object> fields) => fields.ToDictionary(
            kv => kv.Key,
            kv => kv.Value switch
            {
                DateTimeOffset d => (object)Timestamp.FromDateTimeOffset(d),
                _ when ReferenceEquals(kv.Value, DuelCodec.ServerTimestamp) => FieldValue.ServerTimestamp,
                _ => kv.Value,
            });

        static Dictionary<string, object> FromFirestore(Dictionary<string, object> data) => data.ToDictionary(
            kv => kv.Key,
            kv => kv.Value is Timestamp t ? (object)t.ToDateTimeOffset() : kv.Value);

        public void CreateDuel(long seed, int score, Action<Duel> onDone, Action<string> onError)
        {
            SignIn(uid =>
            {
                string code = DuelRules.NewCode(_rng);
                var expiry = DateTimeOffset.UtcNow + Duel.Lifetime;
                string name = DuelRules.DisplayName(_playerName?.Invoke());
                Duels.Document(code).SetAsync(ToFirestore(DuelCodec.CreateFields(seed, uid, name, score, expiry))).ContinueWithOnMainThread(task =>
                {
                    if (task.IsFaulted || task.IsCanceled) { onError?.Invoke(BackendMessage(task.Exception)); return; }
                    _history?.Remember(code);
                    onDone?.Invoke(new Duel
                    {
                        Code = code, Seed = seed, ChallengerUid = uid, ChallengerName = name, ChallengerScore = score, ExpiresAt = expiry,
                    });
                });
            }, e => onError?.Invoke(e));
        }

        public void Fetch(string code, Action<Duel> onDone, Action<string> onError)
        {
            string normalized = DuelRules.Normalize(code);
            if (normalized.Length == 0 || normalized.Contains('/')) { onError?.Invoke(Loc.T("duel.error_not_found")); return; }
            SignIn(_ =>
            {
                Duels.Document(normalized).GetSnapshotAsync().ContinueWithOnMainThread(task =>
                {
                    if (task.IsFaulted || task.IsCanceled) { onError?.Invoke(BackendMessage(task.Exception)); return; }
                    var snapshot = task.Result;
                    var duel = snapshot != null && snapshot.Exists ? DuelCodec.Decode(snapshot.Id, FromFirestore(snapshot.ToDictionary())) : null;
                    if (duel == null) onError?.Invoke(Loc.T("duel.error_not_found"));
                    else onDone?.Invoke(duel);
                });
            }, e => onError?.Invoke(e));
        }

        public void SubmitOpponentScore(string code, int score, Action<Duel> onDone, Action<string> onError)
        {
            Fetch(code, duel =>
            {
                string uid = Uid;
                string refusal = DuelRules.JoinRefusal(duel, uid, DateTimeOffset.UtcNow);
                if (refusal != null) { onError?.Invoke(Loc.T(refusal)); return; }
                string name = DuelRules.DisplayName(_playerName?.Invoke());
                Duels.Document(duel.Code).UpdateAsync(DuelCodec.OpponentFields(uid, name, score)).ContinueWithOnMainThread(task =>
                {
                    if (task.IsFaulted || task.IsCanceled) { onError?.Invoke(BackendMessage(task.Exception)); return; }
                    _history?.Remember(duel.Code);
                    duel.OpponentUid = uid;
                    duel.OpponentName = name;
                    duel.OpponentScore = score;
                    onDone?.Invoke(duel);
                });
            }, onError);
        }

        public void MyDuels(Action<IReadOnlyList<Duel>> onDone)
        {
            var codes = _history?.Codes.ToList() ?? new List<string>();
            var result = new List<Duel>();
            void Next(int i)
            {
                if (i >= codes.Count) { onDone?.Invoke(result); return; }
                Fetch(codes[i], d => { result.Add(d); Next(i + 1); }, _ => Next(i + 1));
            }
            Next(0);
        }
    }
}
#endif
