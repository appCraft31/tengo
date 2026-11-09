using System.Collections.Generic;
using UnityEngine;

namespace TenGo.Fx
{
    /// Actions en cours, attachées à un objet (comme `node.run(action, withKey:)`).
    /// Une action dont le propriétaire est détruit s'arrête d'elle-même.
    public sealed class TweenRunner
    {
        sealed class Entry
        {
            public Object Owner;
            public string Key;
            public Tween Tween;
            public bool Dead;
        }

        readonly List<Entry> _entries = new();
        readonly List<Entry> _added = new();
        readonly Dictionary<Object, float> _speeds = new();
        bool _stepping;

        public int Count => _entries.Count + _added.Count;

        public void Run(Object owner, Tween tween, string key)
        {
            if (key != null) Kill(owner, key);
            var e = new Entry { Owner = owner, Key = key, Tween = tween };
            if (_stepping) _added.Add(e); else _entries.Add(e);
        }

        public void Kill(Object owner, string key = null)
        {
            foreach (var list in new[] { _entries, _added })
                foreach (var e in list)
                    if (!e.Dead && e.Owner == owner && (key == null || e.Key == key))
                        e.Dead = true;
            if (!_stepping) { _entries.RemoveAll(e => e.Dead); _added.RemoveAll(e => e.Dead); }
        }

        public bool Has(Object owner, string key = null)
        {
            foreach (var list in new[] { _entries, _added })
                foreach (var e in list)
                    if (!e.Dead && e.Owner == owner && (key == null || e.Key == key))
                        return true;
            return false;
        }

        /// Vitesse des actions d'un objet (`node.speed`) : 0 fige, 1 normal.
        public void SetSpeed(Object owner, float speed)
        {
            if (Mathf.Approximately(speed, 1f)) _speeds.Remove(owner); else _speeds[owner] = speed;
        }

        public float GetSpeed(Object owner) => owner != null && _speeds.TryGetValue(owner, out var s) ? s : 1f;

        public void Step(float dt)
        {
            _stepping = true;
            try
            {
                for (int i = 0; i < _entries.Count; i++)
                {
                    var e = _entries[i];
                    if (e.Dead) continue;
                    if (e.Owner == null) { e.Dead = true; continue; }
                    float speed = GetSpeed(e.Owner);
                    if (speed <= 0f) continue;
                    try
                    {
                        if (e.Tween.Tick(dt * speed) >= 0f) e.Dead = true;
                    }
                    catch (MissingReferenceException) { e.Dead = true; }
                    catch (System.Exception ex) { Debug.LogException(ex); e.Dead = true; }
                }
            }
            finally
            {
                _stepping = false;
                _entries.RemoveAll(e => e.Dead);
                _entries.AddRange(_added.FindAll(e => !e.Dead));
                _added.Clear();
                if (_speeds.Count > 0)
                {
                    var gone = new List<Object>();
                    foreach (var k in _speeds.Keys) if (k == null) gone.Add(k);
                    foreach (var k in gone) _speeds.Remove(k);
                }
            }
        }

        public void Clear()
        {
            _entries.Clear();
            _added.Clear();
            _speeds.Clear();
        }
    }
}
