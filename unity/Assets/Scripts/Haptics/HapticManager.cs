using System;
using TenGo.Fx;
using UnityEngine;

namespace TenGo.Haptics
{
    /// Retours haptiques du jeu (HapticManager.swift) : réglage persisté,
    /// impacts et motif de validation proportionné à la chaîne. Les délais du
    /// motif courent en temps réel (`asyncAfter`), jamais sur la boucle
    /// d'animation ni sur l'arrêt sur image.
    public static class HapticManager
    {
        static IHaptics _current;

        /// Moteur actif : natif sur appareil, `NullHaptics` en éditeur.
        public static IHaptics Current
        {
            get => _current ??= CreatePlatformDefault();
            set => _current = value;
        }

        /// Réglage « vibrations » (`SettingsRepository.HapticsEnabled`), branché par l'étape 5.
        public static Func<bool> IsEnabled = () => true;

        public static void Bind(Core.SettingsRepository settings) => IsEnabled = () => settings.HapticsEnabled;

        public static IHaptics CreatePlatformDefault()
        {
#if UNITY_IOS && !UNITY_EDITOR
            return new IosHaptics();
#elif UNITY_ANDROID && !UNITY_EDITOR
            return new AndroidHaptics();
#else
            return new NullHaptics();
#endif
        }

        static bool On { get { try { return IsEnabled?.Invoke() ?? true; } catch { return true; } } }

        public static void Prepare() => Current.Prepare();

        /// Impact léger : sélection, boutons de menu.
        public static void Light() { if (On) Current.Impact(HapticStyle.Light, 1f); }

        /// Impact moyen : validation.
        public static void Medium() { if (On) Current.Impact(HapticStyle.Medium, 1f); }

        /// Impact fort : fin des grandes chaînes.
        public static void Heavy() { if (On) Current.Impact(HapticStyle.Heavy, 1f); }

        /// Impact d'intensité continue (la tension du tracé se sent sans paliers).
        public static void Impact(HapticStyle style, float intensity)
        {
            if (On) Current.Impact(style, Mathf.Clamp01(intensity));
        }

        public static void Success() { if (On) Current.Notify(HapticNotification.Success); }
        public static void Warning() { if (On) Current.Notify(HapticNotification.Warning); }

        /// Motif de validation : ≤ 3 → medium 0,7 ; 4-5 → medium 0,9 puis rigid 0,7
        /// à 55 ms ; 6+ → medium 0,6, rigid 0,8 à 45 ms, heavy 1 à 90 ms.
        public static void Chain(int length)
        {
            if (!On) return;
            if (length <= 3)
            {
                Impact(HapticStyle.Medium, 0.7f);
            }
            else if (length <= 5)
            {
                Impact(HapticStyle.Medium, 0.9f);
                FxTime.AfterRealtime(0.055f, () => Impact(HapticStyle.Rigid, 0.7f));
            }
            else
            {
                Impact(HapticStyle.Medium, 0.6f);
                FxTime.AfterRealtime(0.045f, () => Impact(HapticStyle.Rigid, 0.8f));
                FxTime.AfterRealtime(0.090f, () => Impact(HapticStyle.Heavy, 1.0f));
            }
        }

        /// Retour aux valeurs par défaut (tests).
        public static void Reset()
        {
            _current = null;
            IsEnabled = () => true;
        }
    }
}
