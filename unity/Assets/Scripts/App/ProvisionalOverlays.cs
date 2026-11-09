using System;
using System.Collections.Generic;
using TenGo.Audio;
using TenGo.Core;
using TenGo.Fx;
using TenGo.Rendering;
using TenGo.Ui;
using UnityEngine;

namespace TenGo.App
{
    /// Surcouches provisoires de l'étape 5 (les vraies arrivent à l'étape 6).
    public sealed class ProvisionalOverlays : IOverlayFactory
    {
        public IGameOverlay Settings(AppRoot app, Transform parent, Action replayTutorial) =>
            new ProvisionalSettingsOverlay(app, parent, replayTutorial);
    }

    /// Réglages minimaux : son, vibrations, revoir le tutoriel, fermer.
    /// Un tap hors de la carte ferme, comme `SettingsOverlay`.
    public sealed class ProvisionalSettingsOverlay : IGameOverlay
    {
        const int Z = 700;
        readonly Transform _root;
        readonly List<UiButton> _buttons = new();
        readonly AppRoot _app;
        static readonly Vector2 CardSize = new(500, 470);

        bool _open = true;
        public bool IsOpen => _open && _root != null;

        public ProvisionalSettingsOverlay(AppRoot app, Transform parent, Action replayTutorial)
        {
            _app = app;
            var s = app.Services.Settings;
            _root = RenderShared.Node("SettingsOverlay", parent);
            FxAlpha.Of(_root.gameObject).Alpha = 0;
            ShapeView.Rect("Dim", _root, Vector2.zero, new Vector2(1500, 2700), 0, Colors.White(0, 0.4f), default, 0, Z);
            ShapeView.Rect("Card", _root, Vector2.zero, CardSize, 36, new Color(0.97f, 0.95f, 0.92f, 1), Colors.White(0.72f, 0.3f), 1, Z + 1);
            TenGoText.Create(_root, Loc.T("settings.title"), Fonts.AvenirHeavy, 34, Colors.White(0.24f, 1), Units.P(0, 180), Z + 3);

            string Toggle(string key, bool on) => Loc.T(key) + " : " + Loc.T(on ? "settings.toggle_on" : "settings.toggle_off");
            UiButton sound = null, haptics = null;
            sound = Add("sound", Toggle("settings.sound", !s.SoundMuted), 90, () =>
            {
                s.SoundMuted = !s.SoundMuted;
                sound.Label.Text = Toggle("settings.sound", !s.SoundMuted);
            });
            haptics = Add("haptics", Toggle("settings.haptics", s.HapticsEnabled), 0, () =>
            {
                s.HapticsEnabled = !s.HapticsEnabled;
                haptics.Label.Text = Toggle("settings.haptics", s.HapticsEnabled);
            });
            Add("tutorial", Loc.T("settings.replay_tutorial"), -90, () => { Close(); replayTutorial?.Invoke(); });
            Add("close", Loc.T("settings.close"), -180, Close);
            Tw.Run(_root.gameObject, Tw.FadeIn(_root.gameObject, 0.2f));
        }

        UiButton Add(string name, string label, float y, Action onTap)
        {
            var b = UiButton.Create(_root, name, Units.P(0, y), new Vector2(400, 66), 33, null, label, NunitoWeight.Bold, 21,
                Colors.White(0.28f, 1), Z + 2, onTap);
            b.Label.ShrinkToFit(370);
            _buttons.Add(b);
            return b;
        }

        public void HandleTouch(Vector2 pt)
        {
            var world = Units.P(pt.x, pt.y);
            foreach (var b in _buttons) if (b != null && b.Hit(world)) { b.Tap(); _app.FlushSave(); return; }
            if (Mathf.Abs(pt.x) > CardSize.x / 2 || Mathf.Abs(pt.y) > CardSize.y / 2) Close();
        }

        public void Close()
        {
            if (!_open || _root == null) return;
            _open = false;
            var go = _root.gameObject;
            if (Application.isPlaying) UnityEngine.Object.Destroy(go); else UnityEngine.Object.DestroyImmediate(go);
        }
    }
}
