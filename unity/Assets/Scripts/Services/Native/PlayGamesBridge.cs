#if UNITY_ANDROID
using System;
using UnityEngine;

namespace TenGo.Services
{
    /// Play Games Services v2 et Play In-App Review par JNI, sans plugin Unity
    /// (même chemin que PlayGamesManager.kt et ReviewManager.kt) :
    /// `play-services-games-v2` et `review`, déclarés dans
    /// `Editor/TenGoDependencies.xml` et résolus par EDM. L'id de projet Play
    /// Games est la ressource `game_services_project_id` du module
    /// `Plugins/Android/TenGo.androidlib`. Tous les rappels reviennent sur le
    /// fil d'Unity ; toute erreur est journalisée et rend « échec ».
    public static class PlayGamesBridge
    {
        /// Code de requête de l'intent du classement (`RC_LEADERBOARD` Kotlin).
        const int LeaderboardRequestCode = 9004;
        static bool _initialized;

        /// `Task.addOnCompleteListener` : rappel avec la tâche terminée.
        sealed class OnComplete : AndroidJavaProxy
        {
            readonly Action<AndroidJavaObject> _done;
            public OnComplete(Action<AndroidJavaObject> done) : base("com.google.android.gms.tasks.OnCompleteListener") { _done = done; }
            // Appelé par Java sur le fil principal Android.
            public void onComplete(AndroidJavaObject task) => _done(task);
        }

        static void Then(AndroidJavaObject task, Action<AndroidJavaObject> onSuccess, Action<string> onFailure)
        {
            task.Call<AndroidJavaObject>("addOnCompleteListener", new OnComplete(t =>
            {
                bool ok;
                AndroidJavaObject result = null;
                string error = null;
                try
                {
                    ok = t.Call<bool>("isSuccessful");
                    if (ok) result = t.Call<AndroidJavaObject>("getResult");
                    else error = t.Call<AndroidJavaObject>("getException")?.Call<string>("toString");
                }
                catch (Exception e) { ok = false; error = e.Message; }
                MainThread.Run(() =>
                {
                    if (ok) onSuccess(result);
                    else onFailure(error);
                });
            })).Dispose();
        }

        static AndroidJavaObject Activity => NativeBridge.Activity;

        static AndroidJavaClass PlayGames() => new("com.google.android.gms.games.PlayGames");

        /// `PlayGamesSdk.initialize(context)` (TenGoApplication.onCreate).
        public static void Initialize()
        {
#if !UNITY_EDITOR
            if (_initialized) return;
            try
            {
                using var sdk = new AndroidJavaClass("com.google.android.gms.games.PlayGamesSdk");
                using var activity = Activity;
                sdk.CallStatic("initialize", activity);
                _initialized = true;
            }
            catch (Exception e) { Debug.LogWarning("[PlayGames] initialisation : " + e.Message); }
#endif
        }

        /// Connexion silencieuse (`isAuthenticated`) ou interactive (`signIn`),
        /// puis le pseudo du joueur (`PlayersClient.getCurrentPlayer`).
        public static void SignIn(bool interactive, Action<bool, string> done)
        {
#if UNITY_EDITOR
            done(false, null);
#else
            if (!_initialized) Initialize();
            try
            {
                using var games = PlayGames();
                using var activity = Activity;
                using var client = games.CallStatic<AndroidJavaObject>("getGamesSignInClient", activity);
                using var task = client.Call<AndroidJavaObject>(interactive ? "signIn" : "isAuthenticated");
                Then(task, result =>
                {
                    bool ok = result != null && result.Call<bool>("isAuthenticated");
                    result?.Dispose();
                    Debug.Log("[PlayGames] connexion " + (ok ? "réussie" : "absente"));
                    if (!ok) { done(false, null); return; }
                    FetchDisplayName(name => done(true, name));
                }, error =>
                {
                    Debug.Log("[PlayGames] connexion impossible : " + error);
                    done(false, null);
                });
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PlayGames] connexion : " + e.Message);
                done(false, null);
            }
#endif
        }

        static void FetchDisplayName(Action<string> done)
        {
            try
            {
                using var games = PlayGames();
                using var activity = Activity;
                using var players = games.CallStatic<AndroidJavaObject>("getPlayersClient", activity);
                using var task = players.Call<AndroidJavaObject>("getCurrentPlayer");
                Then(task, player =>
                {
                    string name = player?.Call<string>("getDisplayName");
                    player?.Dispose();
                    done(name);
                }, _ => done(null));
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PlayGames] pseudo : " + e.Message);
                done(null);
            }
        }

        public static void SubmitScore(string leaderboardId, long score)
        {
#if !UNITY_EDITOR
            try
            {
                using var games = PlayGames();
                using var activity = Activity;
                using var client = games.CallStatic<AndroidJavaObject>("getLeaderboardsClient", activity);
                client.Call("submitScore", leaderboardId, score);
            }
            catch (Exception e) { Debug.LogWarning("[PlayGames] score : " + e.Message); }
#endif
        }

        public static void ShowLeaderboard(string leaderboardId)
        {
#if !UNITY_EDITOR
            try
            {
                using var games = PlayGames();
                using var activity = Activity;
                using var client = games.CallStatic<AndroidJavaObject>("getLeaderboardsClient", activity);
                using var task = client.Call<AndroidJavaObject>("getLeaderboardIntent", leaderboardId);
                Then(task, intent =>
                {
                    using var current = Activity;
                    current.Call("startActivityForResult", intent, LeaderboardRequestCode);
                    intent.Dispose();
                }, error => Debug.Log("[PlayGames] classement indisponible : " + error));
            }
            catch (Exception e) { Debug.LogWarning("[PlayGames] classement : " + e.Message); }
#endif
        }

        /// Play In-App Review : `requestReviewFlow` puis `launchReviewFlow`
        /// (le quota est géré par Google, rien ne s'affiche s'il est atteint).
        public static void RequestReview()
        {
#if !UNITY_EDITOR
            try
            {
                using var factory = new AndroidJavaClass("com.google.android.play.core.review.ReviewManagerFactory");
                using var activity = Activity;
                var manager = factory.CallStatic<AndroidJavaObject>("create", activity);
                using var task = manager.Call<AndroidJavaObject>("requestReviewFlow");
                Then(task, info =>
                {
                    using var current = Activity;
                    using var launch = manager.Call<AndroidJavaObject>("launchReviewFlow", current, info);
                    info.Dispose();
                    manager.Dispose();
                }, error =>
                {
                    Debug.Log("[Review] indisponible : " + error);
                    manager.Dispose();
                });
            }
            catch (Exception e) { Debug.LogWarning("[Review] " + e.Message); }
#endif
        }
    }
}
#endif
