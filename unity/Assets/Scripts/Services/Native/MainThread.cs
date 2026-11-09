using System;
using System.Collections.Concurrent;
using UnityEngine;

namespace TenGo.Services
{
    /// Ramène sur le fil d'Unity les rappels des SDK natifs (AdMob, UMP, ATT,
    /// IAP, Play Games), qui arrivent sur leurs propres fils. Sans hôte
    /// (éditeur, tests), l'action s'exécute tout de suite.
    public sealed class MainThread : MonoBehaviour
    {
        static MainThread _instance;
        readonly ConcurrentQueue<Action> _queue = new();

        public static void Init(GameObject host)
        {
            if (_instance == null) _instance = host.AddComponent<MainThread>();
        }

        public static void Run(Action action)
        {
            if (action == null) return;
            if (_instance != null) _instance._queue.Enqueue(action);
            else action();
        }

        readonly System.Collections.Generic.List<(Func<bool> done, Action then)> _waits = new();

        /// Exécute `then` dès que `done()` devient vrai (sondage à chaque image) :
        /// demandes d'autorisation des notifications, qui n'ont pas de rappel.
        public static void When(Func<bool> done, Action then)
        {
            if (_instance == null) { if (done()) then(); return; }
            _instance._waits.Add((done, then));
        }

        void Update()
        {
            for (int i = _waits.Count - 1; i >= 0; i--)
            {
                var (done, then) = _waits[i];
                bool ready;
                try { ready = done(); }
                catch (Exception e) { Debug.LogException(e); _waits.RemoveAt(i); continue; }
                if (!ready) continue;
                _waits.RemoveAt(i);
                try { then(); }
                catch (Exception e) { Debug.LogException(e); }
            }
            while (_queue.TryDequeue(out var action))
            {
                try { action(); }
                catch (Exception e) { Debug.LogException(e); }
            }
        }

        void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }
    }
}
