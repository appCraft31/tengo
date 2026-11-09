using System;
using System.Collections.Generic;
using TenGo.App;
using TenGo.Core;
using TenGo.Fx;
using TenGo.Haptics;
using TenGo.Rendering;
using TenGo.Ui;
using TMPro;
using UnityEngine;

namespace TenGo.Screens
{
    /// Réglages (SettingsOverlay.swift), sur l'accueil comme en partie : son,
    /// retours haptiques, effets réduits (grisé si Reduce Motion système),
    /// revoir le tutoriel, notifications, confidentialité, restauration des
    /// achats, noter, partager, support, fermer. Un tap hors d'une ligne ferme.
    public sealed class SettingsOverlay : IGameOverlay
    {
        const int Z = 1000;
        const float CardW = 490, CardH = 820, RowStep = 58;
        static readonly Color CardFill = new(0.98f, 0.96f, 0.93f);
        static readonly Color OnColor = new(0.55f, 0.82f, 0.65f);
        static readonly Color OffColor = Colors.White(0.82f);

        sealed class Toggle
        {
            public ShapeView Bg;
            public Transform Knob;
            public TenGoText Status;
            public TenGoText Label;
        }

        sealed class Row
        {
            public string Name;
            public Transform Node;
            public float Y;
            public Action Action;
        }

        readonly AppRoot _app;
        readonly AppServices _s;
        readonly Action _replayTutorial;
        readonly Transform _root;
        readonly Transform _card;
        readonly List<Row> _rows = new();
        Toggle _sound, _haptic, _effects;
        Transform _alert;
        bool _open = true;

        public bool IsOpen => _open && _root != null;
        public Transform Root => _root;
        /// Message de l'alerte de restauration affichée (tests), sinon `null`.
        public string AlertMessage { get; private set; }

        public SettingsOverlay(AppRoot app, Transform parent, Action replayTutorial)
        {
            _app = app;
            _s = app.Services;
            _replayTutorial = replayTutorial;
            _root = RenderShared.Node("SettingsOverlay", parent);
            FxAlpha.Of(_root.gameObject).Alpha = 0;
            ShapeView.Rect("Dim", _root, Vector2.zero, new Vector2(750 * 2.5f, 1334 * 2.5f), 0, Colors.White(0, 0.55f), default, 0, Z);
            _card = RenderShared.Node("Card", _root);
            BuildUI();
            // `present(in:)` : fondu de 0,18 s, carte de 0,92 à 1 en 0,22 s.
            Tw.Run(_root.gameObject, Tw.FadeIn(_root.gameObject, 0.18f));
            _card.localScale = Vector3.one * 0.92f;
            Tw.Run(_card.gameObject, Tw.Scale(_card, 1f, 0.22f));
        }

        // MARK: - Construction

        void BuildUI()
        {
            ShapeView.Rect("Bg", _card, Vector2.zero, new Vector2(CardW, CardH), 28, CardFill, default, 0, Z + 1);
            TenGoText.Create(_card, Loc.T("settings.title"), Fonts.AvenirHeavy, 30, Colors.White(0.25f), Units.P(0, CardH / 2 - 48), Z + 4)
                .ShrinkToFit(CardW - 140);

            var close = RenderShared.Node("closeBtn", _card, Units.P(CardW / 2 - 32, CardH / 2 - 32));
            ShapeView.Circle("Bg", close, Vector2.zero, 18, Color.white, default, 0, Z + 3);
            TenGoText.Create(close, "×", Fonts.AvenirMedium, 28, Colors.White(0.40f), Units.P(0, 1), Z + 4);
            _rows.Add(new Row { Name = "closeBtn", Node = close, Y = CardH / 2 - 32, Action = Dismiss });

            float y = CardH / 2 - 110;
            _sound = AddToggleRow("row_sound", Loc.T("settings.sound"), y, SettingsIcon.Speaker, new Color(1.00f, 0.96f, 0.62f), ToggleSound);
            y -= RowStep;
            _haptic = AddToggleRow("row_haptic", Loc.T("settings.haptics"), y, SettingsIcon.Phone, new Color(1.00f, 0.72f, 0.86f), ToggleHaptic);
            y -= RowStep;
            _effects = AddToggleRow("row_effects", Loc.T("settings.reduced_effects"), y, SettingsIcon.Sparkles, new Color(0.62f, 0.86f, 1.00f), ToggleEffects);
            y -= RowStep;
            y -= 10;

            AddActionRow("row_tutorial", Loc.T("settings.replay_tutorial"), y, () =>
            {
                Dismiss();
                FxTime.AfterRealtime(0.2f, () => _replayTutorial?.Invoke());
            }); y -= RowStep;
            AddActionRow("row_notifications", Loc.T("settings.notifications"), y, () => _s.Play.OpenSystemSettings()); y -= RowStep;
            AddActionRow("row_privacy", Loc.T("settings.privacy_options"), y, () => _s.Consent.ShowPrivacyOptions()); y -= RowStep;
            // Restauration des achats : exigée par Apple pour le mod « sans pub » (3.1.1).
            AddActionRow("row_restore", Loc.T("settings.restore_purchases"), y, RestorePurchases); y -= RowStep;
            y -= 10;
            AddActionRow("row_rate", Loc.T("settings.rate_app"), y, () => _s.Play.RequestReview()); y -= RowStep;
            AddActionRow("row_share", Loc.T("settings.share_tengo"), y, () => _s.Play.ShareApp()); y -= RowStep;
            AddActionRow("row_support", Loc.T("settings.contact_support"), y, () => _s.Play.OpenSupportMail());

            // Fermeture explicite en bas de carte.
            var accent = Look.BubbleColor(4);
            var closeButton = RenderShared.Node("closeButton", _card, Units.P(0, -CardH / 2 + 88));
            var cbg = ShapeView.Rect("Bg", closeButton, Vector2.zero, new Vector2(CardW - 40, 54), 27, accent, default, 0, Z + 3);
            Relief.Raise(cbg, 4);
            TenGoText.Create(closeButton, Loc.T("settings.close"), Fonts.AvenirBold, 19, accent.ReadableInk(), Vector2.zero, Z + 5)
                .ShrinkToFit(CardW - 80);
            _rows.Add(new Row { Name = "closeButton", Node = closeButton, Y = -CardH / 2 + 88, Action = Dismiss });

            TenGoText.Create(_card, Loc.F("app.version_label", Application.version), Fonts.AvenirMedium, 13, Colors.White(0.55f),
                Units.P(0, -CardH / 2 + 24), Z + 4);

            UpdateSound(false);
            UpdateHaptic(false);
            UpdateEffects(false);
        }

        Transform AddRowPlate(string name, float y)
        {
            var node = RenderShared.Node(name, _card, Units.P(0, y));
            FxAlpha.Of(node.gameObject);
            var size = new Vector2(CardW - 40, 50);
            ShapeView.Rect("Ledge", node, Units.P(0, -3), size, 20, new Color(0.90f, 0.87f, 0.82f), default, 0, Z + 2);
            ShapeView.Rect("Plate", node, Vector2.zero, size, 20, Color.white, default, 0, Z + 3);
            return node;
        }

        Toggle AddToggleRow(string name, string title, float y, SettingsIcon icon, Color tint, Action action)
        {
            var node = AddRowPlate(name, y);
            float chipX = -CardW / 2 + 50;
            ShapeView.Circle("Chip", node, Units.P(chipX, 0), 17, tint, default, 0, Z + 4);
            DrawIcon(node, icon, Units.P(chipX, 0), Colors.White(0.24f), Z + 5);
            var label = TenGoText.Create(node, title, Fonts.AvenirDemiBold, 19, Colors.White(0.26f), Units.P(chipX + 28, 0), Z + 5,
                horizontal: HorizontalAlignmentOptions.Left);
            label.ShrinkToFit(CardW / 2 - 114 + CardW / 2 - chipX - 28 - 70);

            var container = RenderShared.Node(name + "_toggle", node, Units.P(CardW / 2 - 72, 0));
            var bg = ShapeView.Rect("Bg", container, Vector2.zero, new Vector2(64, 32), 16, OffColor, default, 0, Z + 4);
            var knob = RenderShared.Node("Knob", container);
            ShapeView.Circle("Knob", knob, Vector2.zero, 12, Color.white, Colors.White(0.85f), 1, Z + 5);
            var status = TenGoText.Create(node, "", Fonts.AvenirMedium, 12, Colors.White(0.55f), Units.P(CardW / 2 - 114, 0), Z + 5,
                horizontal: HorizontalAlignmentOptions.Right);
            _rows.Add(new Row { Name = name, Node = node, Y = y, Action = action });
            return new Toggle { Bg = bg, Knob = knob, Status = status, Label = label };
        }

        void AddActionRow(string name, string title, float y, Action action)
        {
            var node = AddRowPlate(name, y);
            TenGoText.Create(node, title, Fonts.AvenirDemiBold, 19, Colors.White(0.26f), Units.P(-CardW / 2 + 40, 0), Z + 5,
                horizontal: HorizontalAlignmentOptions.Left).ShrinkToFit(CardW - 120);
            TenGoText.Create(node, "›", Fonts.AvenirMedium, 28, Colors.White(0.55f), Units.P(CardW / 2 - 40, 2), Z + 5,
                horizontal: HorizontalAlignmentOptions.Right);
            _rows.Add(new Row { Name = name, Node = node, Y = y, Action = () => { AnimateRow(node); action(); } });
        }

        // MARK: - Pictogrammes (SF Symbols)

        enum SettingsIcon { Speaker, Phone, Sparkles }

        /// `speaker.wave.2.fill`, `iphone.radiowaves.left.and.right`, `sparkles`, redessinés au trait (19 pt).
        static void DrawIcon(Transform parent, SettingsIcon icon, Vector2 pos, Color color, int order)
        {
            var n = RenderShared.Node("Icon" + icon, parent, pos);
            Vector2 P(float x, float y) => Units.P(x, y);
            switch (icon)
            {
                case SettingsIcon.Speaker:
                    ShapeView.Rect("Box", n, P(-5.5f, 0), new Vector2(4, 6), 1, color, default, 0, order);
                    UiArt.FilledTriangle(n, new[] { P(-5, 0), P(1, 6.5f), P(1, -6.5f) }, color, order);
                    StrokeView.Create("Wave1", n, Arc(P(1.5f, 0), 4f, -50, 50), color, 1.8f, order);
                    StrokeView.Create("Wave2", n, Arc(P(1.5f, 0), 7.5f, -55, 55), color, 1.8f, order);
                    break;
                case SettingsIcon.Phone:
                    ShapeView.Rect("Phone", n, Vector2.zero, new Vector2(7, 12), 2, Color.clear, color, 1.8f, order);
                    StrokeView.Create("L1", n, Arc(Vector2.zero, 6.5f, 140, 220), color, 1.6f, order);
                    StrokeView.Create("L2", n, Arc(Vector2.zero, 9f, 145, 215), color, 1.6f, order);
                    StrokeView.Create("R1", n, Arc(Vector2.zero, 6.5f, -40, 40), color, 1.6f, order);
                    StrokeView.Create("R2", n, Arc(Vector2.zero, 9f, -35, 35), color, 1.6f, order);
                    break;
                default:
                    Sparkle(n, P(-2, -1.5f), 6.5f, color, order);
                    Sparkle(n, P(5, 5), 3.5f, color, order);
                    Sparkle(n, P(5.5f, -5.5f), 2.5f, color, order);
                    break;
            }
        }

        static List<Vector2> Arc(Vector2 c, float rPt, float fromDeg, float toDeg)
        {
            var pts = new List<Vector2>();
            for (int i = 0; i <= 10; i++)
            {
                float a = Mathf.Deg2Rad * Mathf.Lerp(fromDeg, toDeg, i / 10f);
                pts.Add(c + Units.P(Mathf.Cos(a) * rPt, Mathf.Sin(a) * rPt));
            }
            return pts;
        }

        /// Étincelle à quatre branches incurvées (`sparkles`).
        static void Sparkle(Transform parent, Vector2 c, float r, Color color, int order)
        {
            var pts = new List<Vector2>();
            for (int i = 0; i <= 8; i++)
            {
                float a = i * Mathf.PI / 4f + Mathf.PI / 2f;
                float rr = i % 2 == 0 ? r : r * 0.28f;
                pts.Add(c + Units.P(Mathf.Cos(a) * rr, Mathf.Sin(a) * rr));
            }
            StrokeView.Create("Sparkle", parent, pts, color, Mathf.Max(1.2f, r * 0.42f), order);
        }

        // MARK: - Toucher

        public void HandleTouch(Vector2 pt)
        {
            if (!IsOpen) return;
            if (_alert != null)
            {
                // Alerte modale : seul « OK » (ou n'importe où) la ferme.
                RenderShared.Destroy(_alert.gameObject);
                _alert = null;
                AlertMessage = null;
                return;
            }
            var local = (pt - (Vector2)_card.position * Units.PointsPerUnit) / Mathf.Max(0.01f, _card.lossyScale.x);
            foreach (var row in _rows)
            {
                bool hit = row.Name == "closeBtn"
                    ? (local - new Vector2(CardW / 2 - 32, CardH / 2 - 32)).magnitude <= 24
                    : Mathf.Abs(local.x) <= (CardW - 40) / 2 && Mathf.Abs(local.y - row.Y) <= (row.Name == "closeButton" ? 27 : 25);
                if (!hit) continue;
                TapFeedback.Play();
                row.Action?.Invoke();
                return;
            }
            // Ailleurs (voile ou fond de carte) : on ferme, comme iOS.
            Dismiss();
        }

        /// Tap sur une ligne nommée (tests) ; faux si elle n'existe pas.
        public bool TapRow(string name)
        {
            foreach (var row in _rows)
                if (row.Name == name) { row.Action?.Invoke(); return true; }
            return false;
        }

        static void AnimateRow(Transform node)
        {
            var go = node.gameObject;
            Tw.Run(go, Tw.Seq(Tw.Fade(go, 0.4f, 0.08f), Tw.Fade(go, 1f, 0.18f)), "flash");
        }

        public void Close() => Dismiss();

        public void Dismiss()
        {
            if (!_open || _root == null) return;
            _open = false;
            _app.FlushSave();
            var go = _root.gameObject;
            Tw.Run(go, Tw.Seq(Tw.FadeOut(go, 0.15f), Tw.Remove(go)));
        }

        // MARK: - Interrupteurs

        void ToggleSound()
        {
            _s.Settings.SoundMuted = !_s.Settings.SoundMuted;
            _s.Analytics.SoundToggled(!_s.Settings.SoundMuted);
            UpdateSound(true);
        }

        void ToggleHaptic()
        {
            _s.Settings.HapticsEnabled = !_s.Settings.HapticsEnabled;
            if (_s.Settings.HapticsEnabled) HapticManager.Light();
            UpdateHaptic(true);
        }

        void ToggleEffects()
        {
            // Reduce Motion système prioritaire : la ligne est inerte.
            if (JuiceSettings.IsForcedBySystem) return;
            _s.Settings.ReducedEffects = !_s.Settings.ReducedEffects;
            JuiceSettings.InvalidateCache();
            HapticManager.Light();
            UpdateEffects(true);
        }

        void UpdateSound(bool animated)
        {
            bool muted = _s.Settings.SoundMuted;
            Apply(_sound, muted ? OffColor : OnColor, !muted, Loc.T(muted ? "settings.toggle_off" : "settings.toggle_on"), animated);
        }

        void UpdateHaptic(bool animated)
        {
            bool on = _s.Settings.HapticsEnabled;
            Apply(_haptic, on ? OnColor : OffColor, on, Loc.T(on ? "settings.toggle_on" : "settings.toggle_off"), animated);
        }

        void UpdateEffects(bool animated)
        {
            bool forced = JuiceSettings.IsForcedBySystem;
            bool reduced = forced || _s.Settings.ReducedEffects;
            var color = forced ? Colors.White(0.72f) : reduced ? OnColor : OffColor;
            string text = forced ? Loc.T("settings.system_managed") : Loc.T(reduced ? "settings.toggle_on" : "settings.toggle_off");
            _effects.Label.Color = forced ? Colors.White(0.55f) : Colors.White(0.30f);
            Apply(_effects, color, reduced, text, animated);
        }

        static void Apply(Toggle t, Color color, bool on, string status, bool animated)
        {
            float knobX = on ? 16 : -16;
            if (!animated)
            {
                t.Bg.Fill = color;
                t.Knob.localPosition = Units.P(knobX, 0);
                t.Status.Text = status;
                return;
            }
            var from = t.Bg.Fill;
            Tw.Run(t.Bg.gameObject, Tw.Custom(0.18f, k => { if (t.Bg != null) t.Bg.Fill = Color.Lerp(from, color, k); }), "fill");
            t.Status.Text = status;
            Tw.Run(t.Knob.gameObject, Tw.MoveTo(t.Knob, Units.P(knobX, 0), 0.18f), "knob");
        }

        // MARK: - Restauration des achats

        void RestorePurchases()
        {
            _s.CoinStore.RestorePurchases(restored =>
            {
                if (!IsOpen) return;
                _app.FlushSave();
                ShowAlert(Loc.T("settings.restore_purchases"), Loc.T(restored ? "settings.restore_done" : "settings.restore_none"));
            });
        }

        /// `UIAlertController` (titre, message, OK) dessiné dans la scène.
        void ShowAlert(string title, string message)
        {
            if (_alert != null) RenderShared.Destroy(_alert.gameObject);
            AlertMessage = message;
            _alert = RenderShared.Node("Alert", _root);
            FxAlpha.Of(_alert.gameObject).Alpha = 0;
            const int z = Z + 20;
            ShapeView.Rect("Dim", _alert, Vector2.zero, new Vector2(1900, 3400), 0, Colors.White(0, 0.25f), default, 0, z);
            var msg = TenGoText.Create(_alert, message, Fonts.AvenirMedium, 17, Colors.White(0.30f), Vector2.zero, z + 3).Wrap(340);
            float mh = msg.HeightPt();
            float h = 70 + mh + 30 + 56;
            var w = 400f;
            ShapeView.Rect("Bg", _alert, Vector2.zero, new Vector2(w, h), 24, Color.white, default, 0, z + 1);
            TenGoText.Create(_alert, title, Fonts.AvenirBold, 20, Colors.White(0.20f), Units.P(0, h / 2 - 36), z + 3).ShrinkToFit(w - 40);
            msg.transform.localPosition = Units.P(0, h / 2 - 70 - mh / 2);
            ShapeView.Rect("Sep", _alert, Units.P(0, -h / 2 + 56), new Vector2(w, 1), 0, Colors.White(0.85f), default, 0, z + 2);
            TenGoText.Create(_alert, "OK", Fonts.AvenirBold, 19, Look.BubbleColor(4).Darkened(0.35f), Units.P(0, -h / 2 + 28), z + 3);
            Tw.Run(_alert.gameObject, Tw.FadeIn(_alert.gameObject, 0.15f));
        }
    }
}
