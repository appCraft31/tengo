using System;

namespace TenGo.Fx
{
    /// Niveau d'effets visuels (« juice »).
    public enum JuiceLevel
    {
        /// Rendu complet : escalade, secousses, particules, pulsations.
        Full,
        /// Rendu calme : le jeu est strictement identique, seul le ressenti s'apaise.
        Reduced,
    }

    /// Niveau d'effets, croisé avec l'accessibilité système (JuiceSettings.swift).
    /// Point de lecture UNIQUE du réglage « effets réduits » : le gating
    /// Reduce Motion ne peut pas être contourné.
    public static class JuiceSettings
    {
        static JuiceLevel? _cached;
        static bool _forceFull;

        /// Préférence « effets réduits » du joueur (`SettingsRepository.ReducedEffects`),
        /// branchée par l'étape 5 (`Bind`). Faux par défaut.
        public static Func<bool> ReducedPreference = () => false;

        /// Réglage système « Réduire les animations » (`Platform.ReduceMotion`), remplaçable en test.
        public static Func<bool> SystemReduceMotion = Platform.ReduceMotion.IsEnabled;

        /// Mode démo (captures marketing) : le juice reste complet même en Reduce Motion.
        public static bool ForceFull
        {
            get => _forceFull;
            set { _forceFull = value; InvalidateCache(); }
        }

        /// Branche la préférence sur les réglages sauvegardés.
        public static void Bind(Core.SettingsRepository settings)
        {
            ReducedPreference = () => settings.ReducedEffects;
            InvalidateCache();
        }

        /// Mis en cache : lu à chaque effet. À invalider au retour au premier plan
        /// (le réglage système peut changer pendant que l'app tourne) et après un changement de préférence.
        public static JuiceLevel Level
        {
            get
            {
                if (_cached is { } c) return c;
                JuiceLevel v;
                if (_forceFull) v = JuiceLevel.Full;
                else if (SafeSystem() || SafePreference()) v = JuiceLevel.Reduced;
                else v = JuiceLevel.Full;
                _cached = v;
                return v;
            }
        }

        /// Le système impose le rendu calme : le toggle utilisateur est alors grisé.
        public static bool IsForcedBySystem => !_forceFull && SafeSystem();

        /// Mouvement : secousses, écrasement, respiration, comète, confettis.
        public static bool MotionEnabled => Level == JuiceLevel.Full;

        /// Densité de particules (le mode calme sert aussi de filet de sécurité perf).
        public static float ParticleScale => Level == JuiceLevel.Full ? 1f : 0.35f;

        public static void InvalidateCache() => _cached = null;

        /// Retour aux valeurs par défaut (tests, captures).
        public static void Reset()
        {
            ReducedPreference = () => false;
            SystemReduceMotion = Platform.ReduceMotion.IsEnabled;
            _forceFull = false;
            _cached = null;
        }

        static bool SafeSystem() { try { return SystemReduceMotion?.Invoke() ?? false; } catch { return false; } }
        static bool SafePreference() { try { return ReducedPreference?.Invoke() ?? false; } catch { return false; } }
    }
}
