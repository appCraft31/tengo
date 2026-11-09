using System;
using System.Collections.Generic;
using TenGo.Core;
using UnityEngine;

namespace TenGo.Services
{
    /// Services de plateforme simulés : tout est journalisé (`[Play] …`) et
    /// gardé en mémoire pour les tests. L'invitation aux notifications suit le
    /// compteur iOS (`tenGO_notifGamesPlayed`, `tenGO_notifRequested`) sans
    /// afficher de boîte.
    public sealed class LogPlayServices : IPlayServices
    {
        readonly SettingsRepository _settings;
        public readonly List<string> Calls = new();
        bool _signedIn;

        public LogPlayServices(SettingsRepository settings) { _settings = settings; }

        void Log(string s) { Calls.Add(s); Debug.Log("[Play] " + s); }

        public bool IsSignedIn => _signedIn;
        public string PlayerName => null;
        public bool ShowsLeaderboardButtons => true;
        public event Action SignInChanged;

        public void SubmitScore(Leaderboard board, int score) => Log($"score {board} {score}");
        public void ShowLeaderboard(Leaderboard board) => Log($"classement {board}");
        public void RequestReview() => Log("demande d'avis");

        public void RegisterGameAndMaybeRequestNotifications()
        {
            if (_settings != null)
            {
                int count = _settings.NotifGamesPlayed + 1;
                _settings.NotifGamesPlayed = count;
                if (NotificationPlan.ShouldRequest(count, _settings.NotifRequested))
                {
                    _settings.NotifRequested = true;
                    Log("demande d'autorisation des notifications");
                }
            }
            Log("partie comptée pour les notifications");
        }

        public void RefreshNotifications() => Log("notifications reprogrammées");

        public void Authenticate()
        {
            Log("connexion Game Center / Play Games");
            _signedIn = true;
            SignInChanged?.Invoke();
        }

        public void ShareApp() => Log("partage de l'app");
        public void OpenSupportMail() => Log("mail au support");
        public void OpenSystemSettings() => Log("réglages système de l'app");
        public void ShareDuel(string code) => Log("partage du duel " + code);
        public void ScheduleSeedBloom(long bloomAtMs) => Log("notification d'éclosion à " + bloomAtMs);
        public void CancelSeedBloom() => Log("notification d'éclosion annulée");
    }
}
