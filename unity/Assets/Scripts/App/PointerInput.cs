using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using ETouch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace TenGo.App
{
    /// Un seul doigt suivi, comme `touches.first` de SpriteKit : appui,
    /// glissé, relâchement, annulation, en points de scène. Écran tactile par
    /// EnhancedTouch, souris dans l'éditeur, et file d'événements injectés
    /// (tests, pilote automatique).
    public sealed class PointerInput
    {
        public enum Kind { Down, Move, Up, Cancel }

        public readonly struct Event
        {
            public readonly Kind Type;
            public readonly Vector2 Pt;
            public Event(Kind type, Vector2 pt) { Type = type; Pt = pt; }
        }

        readonly Queue<Event> _injected = new();
        int _touchId = -1;
        bool _mouseHeld;
        Vector2 _lastPt;

        /// Faux : ignore l'écran et la souris (tests, pilote).
        public bool ReadDevices { get; set; } = true;

        public void InjectDown(Vector2 pt) => _injected.Enqueue(new Event(Kind.Down, pt));
        public void InjectMove(Vector2 pt) => _injected.Enqueue(new Event(Kind.Move, pt));
        public void InjectUp(Vector2 pt) => _injected.Enqueue(new Event(Kind.Up, pt));
        public void InjectCancel() => _injected.Enqueue(new Event(Kind.Cancel, Vector2.zero));

        public void Clear()
        {
            _injected.Clear();
            _touchId = -1;
            _mouseHeld = false;
        }

        public static void EnableTouch()
        {
            if (!EnhancedTouchSupport.enabled) EnhancedTouchSupport.Enable();
        }

        public void Poll(SceneGeometry g, List<Event> output)
        {
            output.Clear();
            while (_injected.Count > 0) output.Add(_injected.Dequeue());
            if (!ReadDevices) return;

            if (EnhancedTouchSupport.enabled && Touchscreen.current != null)
            {
                foreach (var t in ETouch.activeTouches)
                {
                    var pt = g.ScreenToScene(t.screenPosition);
                    if (_touchId < 0)
                    {
                        if (t.phase != TouchPhase.Began) continue;
                        _touchId = t.touchId;
                        _lastPt = pt;
                        output.Add(new Event(Kind.Down, pt));
                        continue;
                    }
                    if (t.touchId != _touchId) continue;
                    switch (t.phase)
                    {
                        case TouchPhase.Moved:
                            if ((pt - _lastPt).sqrMagnitude > 0.01f) { _lastPt = pt; output.Add(new Event(Kind.Move, pt)); }
                            break;
                        case TouchPhase.Ended:
                            _touchId = -1;
                            output.Add(new Event(Kind.Up, pt));
                            break;
                        case TouchPhase.Canceled:
                            _touchId = -1;
                            output.Add(new Event(Kind.Cancel, pt));
                            break;
                    }
                }
                if (_touchId >= 0) return;
            }

            var mouse = Mouse.current;
            if (mouse == null) return;
            var mp = g.ScreenToScene(mouse.position.ReadValue());
            if (mouse.leftButton.wasPressedThisFrame) { _mouseHeld = true; _lastPt = mp; output.Add(new Event(Kind.Down, mp)); }
            else if (_mouseHeld && mouse.leftButton.wasReleasedThisFrame) { _mouseHeld = false; output.Add(new Event(Kind.Up, mp)); }
            else if (_mouseHeld && (mp - _lastPt).sqrMagnitude > 0.01f) { _lastPt = mp; output.Add(new Event(Kind.Move, mp)); }
        }
    }
}
