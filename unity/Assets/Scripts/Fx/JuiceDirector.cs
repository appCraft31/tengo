using TenGo.Audio;
using TenGo.Haptics;
using TenGo.Rendering;
using UnityEngine;

namespace TenGo.Fx
{
    /// Point d'entrée unique du « juice » (JuiceDirector.swift). La scène ne
    /// connaît que des verbes de jeu (OnSelect, OnCommit, OnWin…) ; durées,
    /// seuils, couleurs et gating d'accessibilité vivent ici.
    ///
    /// RÈGLE D'OR : aucun effet déclenché ici ne touche à l'état du jeu ni n'est
    /// attendu par le chemin critique. Seul l'arrêt sur image ralentit le temps
    /// (`FxTime.Speed`), comme `scene.speed` sur iOS.
    public sealed class JuiceDirector
    {
        const float RejectThrottle = 0.12f;

        readonly Transform _fxLayer;
        readonly ShakeCamera _camera;
        readonly Vector2 _sceneSizePt;
        readonly System.Random _rng;
        int _hitStopToken;
        (int row, int col)? _lastRejectedCell;
        double _lastRejectedAt = double.NegativeInfinity;

        /// La scène décide si un effet plein écran est acceptable (panneau de fin,
        /// réglages ouverts : ils bougeraient avec la caméra).
        public System.Func<bool> AllowsScreenEffects = () => true;

        /// `sceneSizePt` : surface de la scène (750 × 1334 pour le jeu).
        public JuiceDirector(Transform fxLayer, ShakeCamera camera, Vector2 sceneSizePt, System.Random rng = null)
        {
            _fxLayer = fxLayer;
            _camera = camera;
            _sceneSizePt = sceneSizePt;
            _rng = rng ?? new System.Random();
        }

        // MARK: - Tracé

        /// Première bulle d'un chemin.
        public void OnSelect() => HapticManager.Impact(HapticStyle.Soft, 0.35f);

        /// Bulle ajoutée ; `tension` = somme courante / 10. L'haptique part ici,
        /// immédiate et indépendante du son.
        public void OnAppend(float tension) => HapticManager.Impact(HapticStyle.Soft, 0.35f + 0.5f * Mathf.Clamp01(tension));

        /// Bulle refusée parce qu'elle ferait dépasser 10 : « pas celle-là », sans révéler de chiffre.
        public void OnRejected(BubbleView node, int row, int col)
        {
            double now = FxTime.RealNow;
            if (_lastRejectedCell is { } last && last == (row, col) && now - _lastRejectedAt < RejectThrottle) return;
            _lastRejectedCell = (row, col);
            _lastRejectedAt = now;

            SoundManager.Shared.PlayRejected();

            if (!JuiceSettings.MotionEnabled || node == null) return;
            if (Tw.Has(node.gameObject, "reject")) return;
            var t = node.transform;
            Tw.Run(node.gameObject, Tw.Seq(
                Tw.MoveBy(t, Units.P(3, 0), 0.04f),
                Tw.MoveBy(t, Units.P(-6, 0), 0.04f),
                Tw.MoveBy(t, Units.P(3, 0), 0.04f)), "reject");
        }

        // MARK: - Validation

        /// Chaîne validée, en parallèle du chemin critique (jamais attendue).
        public void OnCommit(PopTier tier, int length)
        {
            HapticManager.Chain(length);
            if (!JuiceSettings.MotionEnabled || !AllowsScreenEffects()) return;
            if (_camera != null)
            {
                _camera.Punch(tier.CameraPunch());
                if (tier.ShakeAmplitude() > 0) _camera.Shake(tier.ShakeAmplitude(), tier.ShakeDuration(), _rng);
            }
            if (tier.FlashAlpha() > 0) FlashScreen(tier.FlashAlpha(), 0.06f);
            if (tier.HitStop() > 0) HitStop(tier.HitStop());
        }

        /// Éclatement d'une bulle (`PopEffects.makeBurst` dans la couche d'effets).
        public Transform Burst(Vector2 localPos, Color color, PopTier tier) =>
            PopEffects.MakeBurst(_fxLayer, localPos, color, tier, _rng);

        // MARK: - Arrêt sur image

        void HitStop(float duration)
        {
            int token = ++_hitStopToken;
            FxTime.Speed = 0.04f;
            // Restauration en temps RÉEL : une action serait elle-même ralentie.
            FxTime.AfterRealtime(duration, () => { if (_hitStopToken == token) FxTime.Speed = 1f; });
        }

        /// Filet de sécurité au changement d'état (fin de partie, retour au menu).
        public void ResetTimeScale()
        {
            _hitStopToken++;
            FxTime.Speed = 1f;
        }

        // MARK: - Fin de partie

        public void OnWin(Vector2 center, Color accent)
        {
            HapticManager.Success();
            if (!JuiceSettings.MotionEnabled) return;
            WinCelebration.Make(_fxLayer, center, accent, _sceneSizePt, _rng);
        }

        public void OnLose() => HapticManager.Warning();

        // MARK: - Plein écran

        void FlashScreen(float alpha, float duration)
        {
            var flash = ShapeView.Rect("Flash", _fxLayer, Vector2.zero, _sceneSizePt * 1.4f, 0, Color.white, default, 0, Layers.Flash);
            flash.Additive = true;
            var fa = FxAlpha.Of(flash.gameObject);
            fa.Alpha = 0f;
            Tw.Run(flash.gameObject, Tw.Seq(
                Tw.Fade(flash.gameObject, alpha, duration),
                Tw.FadeOut(flash.gameObject, duration * 2f),
                Tw.Remove(flash.gameObject)));
        }
    }
}
