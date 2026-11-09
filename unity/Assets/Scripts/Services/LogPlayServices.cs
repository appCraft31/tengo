using System.Collections.Generic;
using TenGo.Core;
using UnityEngine;

namespace TenGo.Services
{
    /// Services de plateforme simulés : tout est journalisé (`[Play] …`) et
    /// gardé en mémoire pour les tests. L'invitation aux notifications reprend
    /// le compteur iOS (`tenGO_notifGamesPlayed`) sans afficher de boîte.
    public sealed class LogPlayServices : IPlayServices
    {
        readonly SettingsRepository _settings;
        public readonly List<string> Calls = new();

        public LogPlayServices(SettingsRepository settings) { _settings = settings; }

        void Log(string s) { Calls.Add(s); Debug.Log("[Play] " + s); }

        public void SubmitScore(Leaderboard board, int score) => Log($"score {board} {score}");
        public void ShowLeaderboard(Leaderboard board) => Log($"classement {board}");
        public void RequestReview() => Log("demande d'avis");
        public void RegisterGameAndMaybeRequestNotifications()
        {
            if (_settings != null) _settings.NotifGamesPlayed += 1;
            Log("partie comptée pour les notifications");
        }
        public void Authenticate() => Log("connexion Game Center / Play Games");
        /// Partage de l'app (`AppConfig.shareMessage`), demande d'avis, mail au support, réglages système.
        public void ShareApp() => Log("partage de l'app");
        public void OpenSupportMail() => Log("mail au support");
        public void OpenSystemSettings() => Log("réglages système de l'app");
        public void ShareDuel(string code) => Log("partage du duel " + code);
        public void ScheduleSeedBloom(long bloomAtMs) => Log("notification d'éclosion à " + bloomAtMs);
    }
}
