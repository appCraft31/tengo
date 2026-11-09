#if UNITY_IOS || UNITY_ANDROID
using System;
using TenGo.Core;
using UnityEngine;
#if UNITY_IOS
using Unity.Notifications.iOS;
using UnityEngine.SocialPlatforms;
using UnityEngine.SocialPlatforms.GameCenter;
#else
using Unity.Notifications.Android;
#endif

namespace TenGo.Services
{
    /// Services de plateforme réels (appareil uniquement) :
    /// - classements : Game Center (iOS, API Social d'Unity) ou Play Games
    ///   Services v2 par JNI (Android, comme PlayGamesManager.kt) ;
    /// - avis : SKStoreReviewController / Play In-App Review (JNI) ;
    /// - notifications locales (com.unity.mobile.notifications), calendrier
    ///   de `NotificationPlan`, mêmes identifiants que l'app iOS ;
    /// - partage, mail au support, réglages : `NativeBridge` et `mailto:`.
    public sealed class NativePlayServices : IPlayServices
    {
        public const string SupportEmail = "appcraft31@gmail.com";
        /// Lien App Store des messages de partage (`AppConfig.appStoreURL`).
        public const string AppStoreUrl = "https://apps.apple.com/app/id6762470649";

        readonly SettingsRepository _settings;
        readonly StreakRepository _streak;
        bool _signedIn;
        string _playerName;

        public NativePlayServices(SettingsRepository settings, StreakRepository streak)
        {
            _settings = settings;
            _streak = streak;
#if UNITY_ANDROID
            AndroidNotificationCenter.RegisterNotificationChannel(new AndroidNotificationChannel(
                NotificationPlan.AndroidChannelId, Loc.T("notif.daily_available_title"), Loc.T("notif.daily_available_body"), Importance.Default));
            PlayGamesBridge.Initialize();
#endif
        }

        public bool IsSignedIn => _signedIn;
        public string PlayerName => _playerName;
#if UNITY_IOS
        public bool ShowsLeaderboardButtons => _signedIn;
#else
        public bool ShowsLeaderboardButtons => true;
#endif
        public event Action SignInChanged;

        void SetSignedIn(bool signedIn, string name)
        {
            bool changed = signedIn != _signedIn || name != _playerName;
            _signedIn = signedIn;
            _playerName = string.IsNullOrWhiteSpace(name) ? null : name;
            if (changed) SignInChanged?.Invoke();
        }

        // MARK: Classements

#if UNITY_IOS
        // API Social d'Unity, dépréciée mais seule voie Game Center sans plugin
        // (comme Binero) : à remplacer par le paquet Apple GameKit le jour où elle disparaît.
#pragma warning disable CS0618
        static string GameCenterId(Leaderboard board) => board switch
        {
            Leaderboard.Daily => "com.tengo.leaderboard.daily",
            Leaderboard.Rush => "com.tengo.leaderboard.rush",
            _ => "com.tengo.leaderboard.global",
        };

        public void Authenticate()
        {
            if (Social.localUser.authenticated) { SetSignedIn(true, Social.localUser.userName); return; }
            Social.localUser.Authenticate(ok => MainThread.Run(() =>
            {
                Debug.Log("[GameCenter] connexion " + (ok ? "réussie" : "refusée"));
                SetSignedIn(ok, ok ? Social.localUser.userName : null);
            }));
        }

        public void SubmitScore(Leaderboard board, int score)
        {
            if (!_signedIn || score <= 0) return;
            Social.ReportScore(score, GameCenterId(board), ok => { if (!ok) Debug.Log("[GameCenter] score non envoyé"); });
        }

        public void ShowLeaderboard(Leaderboard board)
        {
            if (!_signedIn) { Authenticate(); return; }
            GameCenterPlatform.ShowLeaderboardUI(GameCenterId(board), TimeScope.AllTime);
        }

        public void RequestReview() => UnityEngine.iOS.Device.RequestStoreReview();
#pragma warning restore CS0618
#else
        static string PlayGamesId(Leaderboard board) => board switch
        {
            Leaderboard.Daily => "CgkIlcfMhaISEAIQAQ",
            Leaderboard.Rush => "CgkIlcfMhaISEAIQBQ",
            _ => "CgkIlcfMhaISEAIQAA",
        };

        /// `signInSilently` puis `fetchDisplayName` (MainActivity.onCreate).
        public void Authenticate() => PlayGamesBridge.SignIn(interactive: false, (ok, name) => SetSignedIn(ok, name));

        public void SubmitScore(Leaderboard board, int score)
        {
            if (!_signedIn || score <= 0) return;
            PlayGamesBridge.SubmitScore(PlayGamesId(board), score);
        }

        /// Non connecté : connexion interactive, puis le classement.
        public void ShowLeaderboard(Leaderboard board)
        {
            if (_signedIn) { PlayGamesBridge.ShowLeaderboard(PlayGamesId(board)); return; }
            PlayGamesBridge.SignIn(interactive: true, (ok, name) =>
            {
                SetSignedIn(ok, name);
                if (ok) PlayGamesBridge.ShowLeaderboard(PlayGamesId(board));
            });
        }

        public void RequestReview() => PlayGamesBridge.RequestReview();
#endif

        // MARK: Notifications (NotificationManager.swift)

        public void RegisterGameAndMaybeRequestNotifications()
        {
            int count = _settings.NotifGamesPlayed + 1;
            _settings.NotifGamesPlayed = count;
            if (NotificationPlan.ShouldRequest(count, _settings.NotifRequested)) RequestAuthorization();
            else RefreshNotifications();
        }

        public void RefreshNotifications()
        {
            if (!IsAuthorized) return;
            ScheduleDailyAvailable();
            RescheduleStreakAtRisk();
        }

        void RequestAuthorization()
        {
            _settings.NotifRequested = true;
#if UNITY_IOS
            var request = new AuthorizationRequest(AuthorizationOption.Alert | AuthorizationOption.Sound | AuthorizationOption.Badge, false);
            MainThread.When(() => request.IsFinished, () =>
            {
                if (request.Granted) { ScheduleDailyAvailable(); RescheduleStreakAtRisk(); }
                request.Dispose();
            });
#else
            // Android 13+ : permission POST_NOTIFICATIONS ; avant, accordée d'office.
            var request = new PermissionRequest();
            MainThread.When(() => request.Status != PermissionStatus.RequestPending, () =>
            {
                if (request.Status == PermissionStatus.Allowed) { ScheduleDailyAvailable(); RescheduleStreakAtRisk(); }
            });
#endif
        }

#if UNITY_IOS
        static bool IsAuthorized => iOSNotificationCenter.GetNotificationSettings().AuthorizationStatus == AuthorizationStatus.Authorized;

        static void Schedule(string id, string title, string body, DateTime fireLocal, bool repeatsDaily)
        {
            iOSNotificationCenter.RemoveScheduledNotification(id);
            var trigger = repeatsDaily
                ? new iOSNotificationCalendarTrigger { Hour = fireLocal.Hour, Minute = fireLocal.Minute, Repeats = true }
                : new iOSNotificationCalendarTrigger
                {
                    Year = fireLocal.Year, Month = fireLocal.Month, Day = fireLocal.Day, Hour = fireLocal.Hour, Minute = fireLocal.Minute, Repeats = false,
                };
            iOSNotificationCenter.ScheduleNotification(new iOSNotification(id)
            {
                Title = title, Body = body, ShowInForeground = false, Trigger = trigger,
            });
        }

        static void Cancel(string id) => iOSNotificationCenter.RemoveScheduledNotification(id);
#else
        static bool IsAuthorized => AndroidNotificationCenter.UserPermissionToPost == PermissionStatus.Allowed;

        static int AndroidId(string id) => id switch
        {
            NotificationPlan.DailyAvailableId => NotificationPlan.AndroidDailyAvailable,
            NotificationPlan.StreakAtRiskId => NotificationPlan.AndroidStreakAtRisk,
            _ => NotificationPlan.AndroidSeedBloom,
        };

        static void Schedule(string id, string title, string body, DateTime fireLocal, bool repeatsDaily)
        {
            int nid = AndroidId(id);
            AndroidNotificationCenter.CancelScheduledNotification(nid);
            var n = new AndroidNotification(title, body, fireLocal);
            if (repeatsDaily) n.RepeatInterval = TimeSpan.FromDays(1);
            AndroidNotificationCenter.SendNotificationWithExplicitID(n, NotificationPlan.AndroidChannelId, nid);
        }

        static void Cancel(string id) => AndroidNotificationCenter.CancelScheduledNotification(AndroidId(id));
#endif

        void ScheduleDailyAvailable()
        {
#if UNITY_IOS
            // Purge de l'ancien rappel générique de la 2.3.0.
            Cancel(NotificationPlan.LegacyReminderId);
#endif
            Schedule(NotificationPlan.DailyAvailableId, Loc.T("notif.daily_available_title"), Loc.T("notif.daily_available_body"),
                NotificationPlan.NextDailyAvailable(DateTime.Now), repeatsDaily: true);
        }

        void RescheduleStreakAtRisk()
        {
            Cancel(NotificationPlan.StreakAtRiskId);
            int streak = _streak.Current;
            if (NotificationPlan.StreakAtRisk(streak, _streak.HasPlayedToday(), DateTime.Now) is not DateTime fire) return;
            Schedule(NotificationPlan.StreakAtRiskId, Loc.F("notif.streak_at_risk_title", streak), Loc.T("notif.streak_at_risk_body"),
                fire, repeatsDaily: false);
        }

        public void ScheduleSeedBloom(long bloomAtMs)
        {
            Cancel(NotificationPlan.SeedBloomId);
            if (!IsAuthorized) return;
            var bloom = DateTimeOffset.FromUnixTimeMilliseconds(bloomAtMs).LocalDateTime;
            Schedule(NotificationPlan.SeedBloomId, Loc.T("notif.seed_bloom_title"), Loc.T("notif.seed_bloom_body"),
                NotificationPlan.SeedBloom(bloom), repeatsDaily: false);
        }

        public void CancelSeedBloom() => Cancel(NotificationPlan.SeedBloomId);

        // MARK: Partage, support, réglages

        public void ShareApp() => NativeBridge.ShareText(Loc.F("app.share_message", AppStoreUrl));

        /// `DuelShare.present(code:)` : message, lien direct, lien de l'app.
        public void ShareDuel(string code) => NativeBridge.ShareText(
            Loc.F("duel.share_message", code) + "\n" + "tengo://duel/" + code + "\n" + Loc.T("duel.share_get_app") + " " + AppStoreUrl);

        public void OpenSupportMail()
        {
#if UNITY_IOS
            const string os = "iOS";
#else
            const string os = "Android";
#endif
            string version = Loc.F("app.version_label", Application.version);
            string body = "\n\n---\n" + version + " • " + os + " " + OsVersion();
            Application.OpenURL("mailto:" + SupportEmail + "?subject=" + Uri.EscapeDataString(Loc.T("settings.support_subject")) +
                                "&body=" + Uri.EscapeDataString(body));
        }

        static string OsVersion()
        {
#if UNITY_IOS
            return UnityEngine.iOS.Device.systemVersion;
#else
            try
            {
                using var version = new AndroidJavaClass("android.os.Build$VERSION");
                return version.GetStatic<string>("RELEASE");
            }
            catch { return SystemInfo.operatingSystem; }
#endif
        }

        public void OpenSystemSettings() => NativeBridge.OpenAppSettings();
    }
}
#endif
