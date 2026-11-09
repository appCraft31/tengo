#if TENGO_FIREBASE
using System;
using System.Collections.Generic;
using Firebase;
using Firebase.Extensions;
using UnityEngine;

namespace TenGo.Services
{
    /// Initialisation unique de Firebase (`FirebaseApp.configure` d'AppDelegate) :
    /// vérifie les services Google Play sur Android, puis rend la main aux
    /// services en attente (Analytics, Auth, Firestore) sur le fil d'Unity.
    /// Lit `GoogleService-Info.plist` / `google-services.json`, copiés dans
    /// Assets/ par `TenGo.Editor.FirebaseConfig` avant chaque build.
    public static class FirebaseBootstrap
    {
        static bool _started;
        static bool? _available;
        static readonly List<Action<bool>> Waiting = new();

        public static bool IsAvailable => _available == true;

        /// `done(disponible)` dès que l'état est connu.
        public static void WhenReady(Action<bool> done)
        {
            if (_available is bool known) { done?.Invoke(known); return; }
            if (done != null) Waiting.Add(done);
            if (_started) return;
            _started = true;
            FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
            {
                bool ok = !task.IsFaulted && !task.IsCanceled && task.Result == DependencyStatus.Available;
                if (!ok) Debug.LogWarning("[Firebase] indisponible : " + (task.IsFaulted ? task.Exception?.GetBaseException().Message : task.Result.ToString()));
                else Debug.Log("[Firebase] prêt");
                _available = ok;
                var waiting = Waiting.ToArray();
                Waiting.Clear();
                foreach (var w in waiting)
                {
                    try { w(ok); }
                    catch (Exception e) { Debug.LogException(e); }
                }
            });
        }
    }
}
#endif
