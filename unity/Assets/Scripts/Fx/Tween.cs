using System;
using System.Collections.Generic;
using UnityEngine;

namespace TenGo.Fx
{
    /// Équivalent d'une `SKAction` : un pas de temps à la fois, composable
    /// (`Tw.Seq`, `Tw.Par`, `Tw.Forever`). Les valeurs de départ sont lues au
    /// premier pas, comme SpriteKit (`fadeAlpha` part de l'alpha courant).
    public abstract class Tween
    {
        /// Avance de `dt` secondes. Retourne le temps non consommé (≥ 0) si
        /// l'action est finie, une valeur négative sinon.
        public abstract float Tick(float dt);

        /// Remet l'action à son début (pour `Forever` et `Repeat`).
        public virtual void Restart() { }

        /// Durée totale (infinie pour `Forever`).
        public abstract float Duration { get; }
    }

    /// Interpolation d'une valeur de `from` (lu au premier pas) à `to`.
    public sealed class ValueTween<T> : Tween
    {
        readonly Func<T> _get;
        readonly Action<T> _set;
        readonly Func<T, T, float, T> _lerp;
        readonly T _to;
        readonly float _duration;
        TwEase _ease;
        T _from;
        float _elapsed;
        bool _started;

        public ValueTween(Func<T> get, Action<T> set, Func<T, T, float, T> lerp, T to, float duration, TwEase ease = TwEase.Linear)
        {
            _get = get; _set = set; _lerp = lerp; _to = to; _duration = Mathf.Max(0f, duration); _ease = ease;
        }

        public override float Duration => _duration;

        /// Change la courbe (par défaut linéaire).
        public ValueTween<T> Ease(TwEase ease) { _ease = ease; return this; }

        public override float Tick(float dt)
        {
            if (!_started) { _from = _get(); _started = true; }
            _elapsed += dt;
            if (_elapsed >= _duration)
            {
                _set(_to);
                return _elapsed - _duration;
            }
            _set(_lerp(_from, _to, TwEasing.Evaluate(_ease, _elapsed / _duration)));
            return -1f;
        }

        public override void Restart() { _elapsed = 0f; _started = false; }
    }

    /// Déplacement relatif (`moveBy`) : appliqué par incréments, il se cumule
    /// avec les autres déplacements du nœud (secousse sur un calque qui bouge).
    public sealed class MoveByTween : Tween
    {
        readonly Transform _target;
        readonly Vector3 _delta;
        readonly float _duration;
        TwEase _ease;
        float _elapsed, _done;

        public MoveByTween(Transform target, Vector3 delta, float duration, TwEase ease = TwEase.Linear)
        {
            _target = target; _delta = delta; _duration = Mathf.Max(0f, duration); _ease = ease;
        }

        public override float Duration => _duration;
        public MoveByTween Ease(TwEase ease) { _ease = ease; return this; }

        public override float Tick(float dt)
        {
            _elapsed += dt;
            float p = _duration <= 0f ? 1f : TwEasing.Evaluate(_ease, _elapsed / _duration);
            if (_target != null) _target.localPosition += _delta * (p - _done);
            _done = p;
            return _elapsed >= _duration ? _elapsed - _duration : -1f;
        }

        public override void Restart() { _elapsed = 0f; _done = 0f; }
    }

    /// Action libre (`customAction`) : reçoit la progression linéaire 0-1 à chaque pas.
    public sealed class CustomTween : Tween
    {
        readonly float _duration;
        readonly Action<float> _step;
        readonly Action _restart;
        float _elapsed;
        public CustomTween(float duration, Action<float> step, Action restart)
        {
            _duration = Mathf.Max(0f, duration); _step = step; _restart = restart;
        }
        public override float Duration => _duration;
        public override float Tick(float dt)
        {
            _elapsed += dt;
            float p = _duration <= 0f ? 1f : Mathf.Clamp01(_elapsed / _duration);
            _step?.Invoke(p);
            return _elapsed >= _duration ? _elapsed - _duration : -1f;
        }
        public override void Restart() { _elapsed = 0f; _restart?.Invoke(); }
    }

    /// Attente (`wait(forDuration:)`).
    public sealed class WaitTween : Tween
    {
        readonly float _duration;
        float _elapsed;
        public WaitTween(float duration) { _duration = Mathf.Max(0f, duration); }
        public override float Duration => _duration;
        public override float Tick(float dt)
        {
            _elapsed += dt;
            return _elapsed >= _duration ? _elapsed - _duration : -1f;
        }
        public override void Restart() => _elapsed = 0f;
    }

    /// Rappel instantané (`run { }`).
    public sealed class CallTween : Tween
    {
        readonly Action _action;
        public CallTween(Action action) { _action = action; }
        public override float Duration => 0f;
        public override float Tick(float dt) { _action?.Invoke(); return dt; }
    }

    /// Série (`sequence`) : le reste de temps d'une action passe à la suivante.
    public sealed class SeqTween : Tween
    {
        readonly List<Tween> _items;
        int _index;
        public SeqTween(IEnumerable<Tween> items) { _items = new List<Tween>(items); }
        public override float Duration { get { float d = 0f; foreach (var t in _items) d += t.Duration; return d; } }

        public override float Tick(float dt)
        {
            while (_index < _items.Count)
            {
                float left = _items[_index].Tick(dt);
                if (left < 0f) return -1f;
                _index++;
                dt = left;
            }
            return dt;
        }

        public override void Restart()
        {
            _index = 0;
            foreach (var t in _items) t.Restart();
        }
    }

    /// Parallèle (`group`) : fini quand la plus longue est finie.
    public sealed class ParTween : Tween
    {
        readonly List<Tween> _items;
        readonly bool[] _done;
        readonly float[] _left;
        public ParTween(IEnumerable<Tween> items)
        {
            _items = new List<Tween>(items);
            _done = new bool[_items.Count];
            _left = new float[_items.Count];
        }
        public override float Duration { get { float d = 0f; foreach (var t in _items) d = Mathf.Max(d, t.Duration); return d; } }

        public override float Tick(float dt)
        {
            bool all = true;
            float minLeft = float.MaxValue;
            for (int i = 0; i < _items.Count; i++)
            {
                if (!_done[i])
                {
                    float left = _items[i].Tick(dt);
                    if (left >= 0f) { _done[i] = true; _left[i] = left; }
                    else { all = false; continue; }
                }
                else _left[i] += dt;
                minLeft = Mathf.Min(minLeft, _left[i]);
            }
            if (!all) return -1f;
            return _items.Count == 0 ? dt : minLeft;
        }

        public override void Restart()
        {
            for (int i = 0; i < _items.Count; i++) { _done[i] = false; _left[i] = 0f; _items[i].Restart(); }
        }
    }

    /// Répétition `count` fois, ou sans fin si `count` < 0 (`repeatForever`).
    public sealed class RepeatTween : Tween
    {
        readonly Tween _inner;
        readonly int _count;
        int _iteration;
        public RepeatTween(Tween inner, int count) { _inner = inner; _count = count; }
        public override float Duration => _count < 0 ? float.PositiveInfinity : _inner.Duration * _count;

        public override float Tick(float dt)
        {
            // Garde-fou : une action de durée nulle répétée sans fin ne bloque pas la boucle.
            int guard = 0;
            while (_count < 0 || _iteration < _count)
            {
                float left = _inner.Tick(dt);
                if (left < 0f) return -1f;
                _iteration++;
                _inner.Restart();
                dt = left;
                if (++guard > 64 || dt <= 0f) return (_count >= 0 && _iteration >= _count) ? dt : -1f;
            }
            return dt;
        }

        public override void Restart() { _iteration = 0; _inner.Restart(); }
    }
}
