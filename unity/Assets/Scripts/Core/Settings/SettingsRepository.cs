#nullable enable
namespace TenGo.Core
{
    /// Réglages et petits drapeaux persistants (SoundManager, HapticManager,
    /// JuiceSettings, tutoriel, notifications), aux clés d'iOS.
    public sealed class SettingsRepository
    {
        private readonly ISaveStore _store;

        public SettingsRepository(ISaveStore store)
        {
            _store = store;
        }

        public bool SoundMuted
        {
            get => _store.GetBool(SaveKeys.SoundMuted);
            set => _store.SetBool(SaveKeys.SoundMuted, value);
        }

        /// Activé par défaut (clé absente = vrai, comme iOS).
        public bool HapticsEnabled
        {
            get => _store.GetBool(SaveKeys.HapticsEnabled, true);
            set => _store.SetBool(SaveKeys.HapticsEnabled, value);
        }

        /// Rendu calme : coupe secousses, particules lourdes et pulsations.
        public bool ReducedEffects
        {
            get => _store.GetBool(SaveKeys.ReducedEffects);
            set => _store.SetBool(SaveKeys.ReducedEffects, value);
        }

        public bool HasSeenTutorial
        {
            get => _store.GetBool(SaveKeys.HasSeenTutorial);
            set => _store.SetBool(SaveKeys.HasSeenTutorial, value);
        }

        /// Parties jouées (pour différer la demande de notifications).
        public int NotifGamesPlayed
        {
            get => _store.GetInt(SaveKeys.NotifGamesPlayed);
            set => _store.SetInt(SaveKeys.NotifGamesPlayed, value);
        }

        /// L'autorisation de notifications a déjà été demandée.
        public bool NotifRequested
        {
            get => _store.GetBool(SaveKeys.NotifRequested);
            set => _store.SetBool(SaveKeys.NotifRequested, value);
        }
    }
}
