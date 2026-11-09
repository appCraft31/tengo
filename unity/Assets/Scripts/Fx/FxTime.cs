using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TenGo.Fx
{
    /// Horloge commune des animations (tweens, particules, fonds). En jeu, un
    /// `FxDriver` caché l'avance à chaque image avec `Time.unscaledDeltaTime`
    /// (les `SKAction` tournent aussi pendant la pause) ; en éditeur (captures,
    /// tests), on l'avance à la main par `Step` ou `Advance`, ce qui rend les
    /// images reproductibles.
    public static class FxTime
    {
        /// Pas maximal d'une image (évite un saut après un gel de l'application).
        public const float MaxStep = 0.1f;

        static readonly List<IFxTickable> _tickables = new();
        static readonly List<IFxTickable> _pending = new();
        static readonly List<IFxTickable> _snapshot = new();
        static readonly List<(double at, Action action)> _realtime = new();
        static readonly List<Action> _due = new();
        static bool _ticking;

        /// Temps d'animation écoulé (ralenti par `Speed`), en secondes.
        public static double Now { get; private set; }

        /// Temps réel écoulé (insensible à `Speed`), en secondes.
        public static double RealNow { get; private set; }

        /// Vrai si le pilote d'image est actif (mode jeu).
        public static bool AutoDriven { get; private set; }

        /// `scene.speed` : 1 = normal, 0,04 pendant un arrêt sur image (JuiceDirector).
        /// Ralentit les actions, les particules et le temps des shaders.
        public static float Speed { get; set; } = 1f;

        static readonly int TimeId = Shader.PropertyToID("_TenGoTime");

        /// Pousse `Now` vers les shaders (`_TenGoTime`, TenGoCommon.hlsl), modulo une heure.
        static void PublishTime() => Shader.SetGlobalFloat(TimeId, (float)(Now % 3600.0));

        public static void Register(IFxTickable t)
        {
            EnsureDriver();
            if (_ticking) _pending.Add(t); else _tickables.Add(t);
        }

        public static void Unregister(IFxTickable t)
        {
            _tickables.Remove(t);
            _pending.Remove(t);
        }

        /// Exécute `action` après `seconds` de temps **réel** (`DispatchQueue.main.asyncAfter`) :
        /// sert à rendre la main après un arrêt sur image, que `Speed` ralentirait.
        public static void AfterRealtime(float seconds, Action action)
        {
            EnsureDriver();
            _realtime.Add((RealNow + Mathf.Max(0f, seconds), action));
        }

        /// Avance toutes les animations de `dt` secondes réelles.
        public static void Step(float dt)
        {
            if (dt <= 0f) return;
            RealNow += dt;
            RunDueRealtime();
            float scaled = dt * Mathf.Max(0f, Speed);
            if (scaled <= 0f) return;
            Now += scaled;
            PublishTime();
            Tw.Runner.Step(scaled);
            _ticking = true;
            _snapshot.Clear();
            _snapshot.AddRange(_tickables);
            foreach (var t in _snapshot)
            {
                if (!_tickables.Contains(t)) continue;   // retiré pendant ce pas
                bool keep;
                try { keep = t != null && !(t is Object o && o == null) && t.FxTick(scaled); }
                catch (Exception e) { Debug.LogException(e); keep = false; }
                if (!keep) _tickables.Remove(t);
            }
            _snapshot.Clear();
            _ticking = false;
            if (_pending.Count > 0) { _tickables.AddRange(_pending); _pending.Clear(); }
        }

        static void RunDueRealtime()
        {
            if (_realtime.Count == 0) return;
            _due.Clear();
            for (int i = _realtime.Count - 1; i >= 0; i--)
            {
                if (_realtime[i].at > RealNow + 1e-9) continue;
                _due.Add(_realtime[i].action);
                _realtime.RemoveAt(i);
            }
            for (int i = _due.Count - 1; i >= 0; i--)
            {
                try { _due[i]?.Invoke(); }
                catch (Exception e) { Debug.LogException(e); }
            }
            _due.Clear();
        }

        /// Avance de `seconds` par pas de `step` (captures « à l'instant t »).
        public static void Advance(float seconds, float step = 1f / 60f)
        {
            while (seconds > 1e-6f)
            {
                float dt = Mathf.Min(step, seconds);
                Step(dt);
                seconds -= dt;
            }
        }

        /// Oublie tout (changement de scène de test, captures).
        public static void Reset()
        {
            _tickables.Clear();
            _pending.Clear();
            _realtime.Clear();
            Tw.Runner.Clear();
            Now = 0;
            RealNow = 0;
            Speed = 1f;
            PublishTime();
        }

        internal static void EnsureDriver()
        {
            if (AutoDriven || !Application.isPlaying) return;
            var go = new GameObject("[FxDriver]") { hideFlags = HideFlags.HideAndDontSave };
            Object.DontDestroyOnLoad(go);
            go.AddComponent<FxDriver>();
            AutoDriven = true;
        }

        internal static void DriverDestroyed() => AutoDriven = false;
    }
}
