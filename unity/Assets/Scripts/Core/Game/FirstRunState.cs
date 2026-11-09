#nullable enable
using System;

namespace TenGo.Core
{
    /// Parcours du tout premier lancement (FirstRun.swift, fiche TEN-4). Tant
    /// qu'aucune partie n'est terminée depuis l'installation : tutoriel court,
    /// ATT / Game Center / pubs après la première partie, pas de demande d'avis,
    /// interstitielles plus tardives. Mesure `first_game_start` puis
    /// `first_game_complete`, une seule fois par installation.
    public sealed class FirstRunState
    {
        private const string Pending = "pending";
        private const string Done = "done";

        private readonly ISaveStore _store;
        private readonly IClock _clock;
        private readonly IGameAnalytics? _analytics;

        public FirstRunState(ISaveStore store, IClock clock, IGameAnalytics? analytics)
        {
            _store = store;
            _clock = clock;
            _analytics = analytics;
        }

        /// L'état valait « pending » au lancement : première session de jeu,
        /// jusqu'au prochain lancement à froid.
        public bool IsFirstSession { get; private set; }

        /// Aucune partie terminée depuis l'installation.
        public bool IsPending => _store.GetString(SaveKeys.FirstRunState) == Pending;

        /// Au lancement, avant tout écran. Un joueur installé avant cette version
        /// (tutoriel vu, parties comptées, scores) est classé « done » sans mesure.
        public void Bootstrap()
        {
            if (_store.GetString(SaveKeys.FirstRunState) == null)
            {
                bool hasHistory = _store.GetBool(SaveKeys.HasSeenTutorial)
                    || _store.GetInt(SaveKeys.NotifGamesPlayed) > 0
                    || (_store.GetIntList(SaveKeys.HighScores)?.Count ?? 0) > 0;
                _store.SetString(SaveKeys.FirstRunState, hasHistory ? Done : Pending);
                if (!hasHistory) _store.SetDouble(SaveKeys.FirstOpenAt, SwiftDate.ToReferenceSeconds(_clock.Now));
            }
            IsFirstSession = IsPending;
        }

        /// Secondes depuis la première ouverture (-1 si inconnue).
        public long SecondsSinceFirstOpen
        {
            get
            {
                if (!_store.Has(SaveKeys.FirstOpenAt)) return -1;
                DateTimeOffset first = SwiftDate.FromReferenceSeconds(_store.GetDouble(SaveKeys.FirstOpenAt));
                return Math.Max(0, (long)(_clock.Now - first).TotalSeconds);
            }
        }

        /// Début d'une partie : ne journalise que la première de l'installation.
        public void GameStarted(string mode)
        {
            if (!IsPending || _store.GetBool(SaveKeys.FirstGameStartLogged)) return;
            _store.SetBool(SaveKeys.FirstGameStartLogged, true);
            _analytics?.FirstGameStart(mode, SecondsSinceFirstOpen);
        }

        /// Fin d'une partie : la première de l'installation journalise
        /// `first_game_complete` et passe l'état à « done ». Vrai dans ce cas.
        public bool GameCompleted(string mode, int score, bool won)
        {
            if (!IsPending) return false;
            long seconds = SecondsSinceFirstOpen;
            _store.SetString(SaveKeys.FirstRunState, Done);
            _analytics?.FirstGameComplete(mode, score, won, seconds);
            return true;
        }
    }
}
