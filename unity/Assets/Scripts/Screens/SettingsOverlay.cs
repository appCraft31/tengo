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
    /// Réglages (SettingsOverlay.swift), sur l'accueil comme en partie.
    /// Refonte 4.0 (`design/refonte/06_autres`) : plein écran opaque au lieu
    /// d'une modale, retour en haut à gauche (« closeButton », alias
    /// « closeBtn »), lignes groupées Jeu (son, vibrations, effets réduits —
    /// grisé si Reduce Motion système —, notifications), Compte (restaurer
    /// les achats, confidentialité) et Aide (revoir le tutoriel, noter,
    /// partager, support), interrupteurs aux couleurs du système, version.
    public sealed class SettingsOverlay : IGameOverlay
    {
        const int Z = 1000;

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
            public Vector2 Center;
            public Vector2 Size;
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
        float _w, _rowH;

        public bool IsOpen => _open && _root != null;
        public Transform Root => _root;
        /// Message de l'alerte de restauration affichée (tests), sinon `null`.
        public string AlertMessage { get; private set; }

        static float Pt(float v) => Design.Pt(v);

        public SettingsOverlay(AppRoot app, Transform parent, Action replayTutorial)
        {
            _app = app;
            _s = app.Services;
            _replayTutorial = replayTutorial;
            _root = RenderShared.Node("SettingsOverlay", parent);
            FxAlpha.Of(_root.gameObject).Alpha = 0;
            ShapeView.Rect("Bg", _root, Vector2.zero, new Vector2(750 * 2.5f, 1334 * 2.5f), 0, Design.Bg, default, 0, Z);
            _card = RenderShared.Node("Card", _root);
            BuildUI();
            // Entrée : fondu de 0,18 s et léger glissement depuis la droite.
            Tw.Run(_root.gameObject, Tw.FadeIn(_root.gameObject, 0.18f));
            _card.localPosition = Units.P(Pt(24), 0);
            Tw.Run(_card.gameObject, Tw.MoveTo(_card, Vector3.zero, 0.22f).Ease(TwEase.EaseOut));
        }

        // MARK: - Construction

        void BuildUI()
        {
            var geo = _app.Geometry;
            float usableW = Mathf.Min(geo.VisibleWidth, SceneGeometry.RefWidth);
            float halfH = Mathf.Min(geo.VisibleHeight, SceneGeometry.RefHeight) / 2f;
            float bottomInset = Mathf.Max(geo.SafeBottomPx, 20f * geo.PixelsPerPoint) / geo.PixelsPerScene;
            _w = usableW - 2 * Pt(Design.Gutter);
            float left = -_w / 2;

            float rowY = halfH - geo.TopInset - Pt(32);
            var back = Design.BackHeader(_card, left, -left, rowY, Loc.T("settings.title"), null, Z + 4, out _);
            back.name = "closeButton";
            AddRow("closeButton", back, new Vector2(left + Pt(22), rowY), Design.Pt(52, 52), Dismiss);
            AddRow("closeBtn", back, new Vector2(left + Pt(22), rowY), Design.Pt(52, 52), Dismiss);

            // Hauteur des lignes : 56 pt, tassée si l'écran est court.
            float top = halfH - geo.TopInset - Pt(64);
            float bottom = -halfH + bottomInset + Pt(24);
            float fixedH = 3 * Pt(36) + 2 * Pt(10) + Pt(4);
            _rowH = Mathf.Clamp((top - bottom - fixedH) / 10f, Pt(44), Pt(56));
            float y = top;

            y = Group("ui.settings.group_game", y);
            float cardTop = y;
            _sound = AddToggleRow("row_sound", Loc.T("settings.sound"), ref y, VectorIconName.LineBolt, 3, ToggleSound);
            _haptic = AddToggleRow("row_haptic", Loc.T("settings.haptics"), ref y, VectorIconName.LineStar, 9, ToggleHaptic);
            _effects = AddToggleRow("row_effects", Loc.T("settings.reduced_effects"), ref y, VectorIconName.LineSeed, 4, ToggleEffects);
            AddActionRow("row_notifications", Loc.T("settings.notifications"), ref y, VectorIconName.LineCalendar, 8, () => _s.Play.OpenSystemSettings());
            GroupCard(cardTop, y);

            y = Group("ui.settings.group_account", y - Pt(10));
            cardTop = y;
            // Restauration des achats : exigée par Apple pour le mod « sans pub » (3.1.1).
            AddActionRow("row_restore", Loc.T("settings.restore_purchases"), ref y, VectorIconName.LineShop, 5, RestorePurchases);
            AddActionRow("row_privacy", Loc.T("settings.privacy_options"), ref y, VectorIconName.LineLock, 7, () => _s.Consent.ShowPrivacyOptions());
            GroupCard(cardTop, y);

            y = Group("ui.settings.group_help", y - Pt(10));
            cardTop = y;
            AddActionRow("row_tutorial", Loc.T("settings.replay_tutorial"), ref y, VectorIconName.LinePlay, 2, () =>
            {
                Dismiss();
                FxTime.AfterRealtime(0.2f, () => _replayTutorial?.Invoke());
            });
            AddActionRow("row_rate", Loc.T("settings.rate_app"), ref y, VectorIconName.LineStar, 3, () => _s.Play.RequestReview());
            AddActionRow("row_share", Loc.T("settings.share_tengo"), ref y, VectorIconName.LineShare, 6, () => _s.Play.ShareApp());
            AddActionRow("row_support", Loc.T("settings.contact_support"), ref y, VectorIconName.LineVideo, 1, () => _s.Play.OpenSupportMail());
            GroupCard(cardTop, y);

            Design.Text(_card, Loc.F("app.version_label", Application.version), NunitoWeight.Bold, 12, Design.Ink3,
                new Vector2(0, Mathf.Max(bottom - Pt(8), y - Pt(18))), Z + 4);

            UpdateSound(false);
            UpdateHaptic(false);
            UpdateEffects(false);
        }

        /// Titre de groupe en petites capitales ; renvoie le haut de la carte du groupe.
        float Group(string key, float y)
        {
            Design.Text(_card, Loc.T(key).ToUpperInvariant(), NunitoWeight.Black, 12, Design.Ink2, new Vector2(-_w / 2 + Pt(4), y - Pt(18)), Z + 4,
                HorizontalAlignmentOptions.Left);
            return y - Pt(36);
        }

        void GroupCard(float top, float bottom) =>
            Design.Surface("Group", _card, new Vector2(0, (top + bottom) / 2), new Vector2(_w, top - bottom), Pt(Design.CardRadius), Z + 1);

        void AddRow(string name, Transform node, Vector2 center, Vector2 size, Action action) =>
            _rows.Add(new Row { Name = name, Node = node, Center = center, Size = size, Action = action });

        /// Ligne : pastille d'icône teintée et libellé (`label`) ; renvoie le nœud.
        Transform RowPlate(string name, string title, ref float y, VectorIconName icon, int tint, bool separator, out TenGoText label)
        {
            float cy = y - _rowH / 2;
            var node = RenderShared.Node(name, _card, Units.P(0, cy));
            FxAlpha.Of(node.gameObject);
            if (separator) ShapeView.Rect("Sep", node, Units.P(0, _rowH / 2), new Vector2(_w, Pt(1)), 0, Design.Line, default, 0, Z + 3);
            float lx = -_w / 2 + Pt(14);
            Design.IconChip(node, new Vector2(lx + Pt(17), 0), 34, 11, Look.BubbleColor(tint), icon, Z + 3, Design.Ink, 18);
            label = Design.Text(node, title, NunitoWeight.ExtraBold, 16, Design.Ink, new Vector2(lx + Pt(34 + 12), 0), Z + 4,
                HorizontalAlignmentOptions.Left);
            y -= _rowH;
            return node;
        }

        Toggle AddToggleRow(string name, string title, ref float y, VectorIconName icon, int tint, Action action)
        {
            bool first = name == "row_sound";
            float cy = y - _rowH / 2;
            var node = RowPlate(name, title, ref y, icon, tint, !first, out var label);
            float lx = -_w / 2 + Pt(14 + 34 + 12), rx = _w / 2 - Pt(14);
            label.ShrinkToFit(rx - Pt(51 + 12) - lx);

            var container = RenderShared.Node(name + "_toggle", node, Units.P(rx - Pt(25.5f), 0));
            var bg = ShapeView.Rect("Bg", container, Vector2.zero, Design.Pt(51, 31), Pt(15.5f), Design.Line, default, 0, Z + 4);
            var knob = RenderShared.Node("Knob", container);
            ShapeView.Circle("Knob", knob, Vector2.zero, Pt(13.5f), Design.Card, default, 0, Z + 5);
            var status = Design.Text(node, "", NunitoWeight.Bold, 11, Design.Ink3, new Vector2(rx - Pt(51 + 10), 0), Z + 5,
                HorizontalAlignmentOptions.Right);
            AddRow(name, node, new Vector2(0, cy), new Vector2(_w, _rowH), action);
            return new Toggle { Bg = bg, Knob = knob, Status = status, Label = label };
        }

        void AddActionRow(string name, string title, ref float y, VectorIconName icon, int tint, Action action)
        {
            bool first = name == "row_restore" || name == "row_tutorial";
            float cy = y - _rowH / 2;
            var node = RowPlate(name, title, ref y, icon, tint, !first, out var label);
            float lx = -_w / 2 + Pt(14 + 34 + 12), rx = _w / 2 - Pt(14);
            label.ShrinkToFit(rx - Pt(30) - lx);
            Design.Chevron(node, new Vector2(rx - Pt(9), 0), Z + 4, 18, Design.Ink3);
            AddRow(name, node, new Vector2(0, cy), new Vector2(_w, _rowH), () => { AnimateRow(node); action(); });
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
                if (Mathf.Abs(local.x - row.Center.x) > row.Size.x / 2 || Mathf.Abs(local.y - row.Center.y) > row.Size.y / 2) continue;
                TapFeedback.Play();
                row.Action?.Invoke();
                return;
            }
            // Plein écran : un tap hors d'une ligne ne ferme plus.
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

        static Color OnColor => Design.Accent;
        static Color OffColor => Design.Line;

        void UpdateSound(bool animated)
        {
            bool muted = _s.Settings.SoundMuted;
            Apply(_sound, muted ? OffColor : OnColor, !muted, "", animated);
        }

        void UpdateHaptic(bool animated)
        {
            bool on = _s.Settings.HapticsEnabled;
            Apply(_haptic, on ? OnColor : OffColor, on, "", animated);
        }

        void UpdateEffects(bool animated)
        {
            bool forced = JuiceSettings.IsForcedBySystem;
            bool reduced = forced || _s.Settings.ReducedEffects;
            var color = forced ? Design.Tint(Design.Accent, 0.45f) : reduced ? OnColor : OffColor;
            _effects.Label.Color = forced ? Design.Ink3 : Design.Ink;
            Apply(_effects, color, reduced, forced ? Loc.T("settings.system_managed") : "", animated);
        }

        static void Apply(Toggle t, Color color, bool on, string status, bool animated)
        {
            float knobX = Design.Pt(on ? 10 : -10);
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
            ShapeView.Rect("Dim", _alert, Vector2.zero, new Vector2(1900, 3400), 0, Design.Ink.WithAlpha(0.25f), default, 0, z);
            var msg = TenGoText.Create(_alert, message, NunitoWeight.Bold, 17, Design.Ink2, Vector2.zero, z + 3).Wrap(340);
            float mh = msg.HeightPt();
            float h = 70 + mh + 30 + 56;
            var w = 400f;
            ShapeView.Rect("Bg", _alert, Vector2.zero, new Vector2(w, h), 28, Design.Card, default, 0, z + 1);
            TenGoText.Create(_alert, title, NunitoWeight.Black, 20, Design.Ink, Units.P(0, h / 2 - 36), z + 3).ShrinkToFit(w - 40);
            msg.transform.localPosition = Units.P(0, h / 2 - 70 - mh / 2);
            ShapeView.Rect("Sep", _alert, Units.P(0, -h / 2 + 56), new Vector2(w, 1), 0, Design.Line, default, 0, z + 2);
            TenGoText.Create(_alert, "OK", NunitoWeight.Black, 19, Design.SeedGreen, Units.P(0, -h / 2 + 28), z + 3);
            Tw.Run(_alert.gameObject, Tw.FadeIn(_alert.gameObject, 0.15f));
        }
    }
}
