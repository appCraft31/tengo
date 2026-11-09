using TenGo.App;
using TenGo.Core;
using TenGo.Fx;
using TenGo.Rendering;
using TenGo.Services;
using UnityEngine;

namespace TenGo.Screens
{
    /// Classement local (LeaderboardScene.swift) : fond crème fixe à bulles
    /// pastel flottantes, grande bulle du meilleur score, pilules des rangs 2
    /// à 7, puis les classements en ligne (Défi du jour, Mondial, Rush).
    /// Cibles : « dailyLeaderboard », « worldLeaderboard », « rushLeaderboard », « back ».
    public sealed class LeaderboardScreen : ScreenBase
    {
        const int Z = 20;
        static readonly Color Cream = new(0.97f, 0.95f, 0.92f);

        static readonly Color[] BubbleColors =
        {
            new(0.98f, 0.72f, 0.68f), new(0.99f, 0.84f, 0.70f), new(0.99f, 0.95f, 0.72f),
            new(0.78f, 0.94f, 0.82f), new(0.72f, 0.88f, 0.98f), new(0.82f, 0.78f, 0.97f),
            new(0.98f, 0.78f, 0.88f), new(0.80f, 0.91f, 0.80f), new(0.76f, 0.82f, 0.97f),
        };

        /// Arc-en-ciel pastel par rang : rose, ciel, menthe, jaune, lavande, pêche, sauge.
        static readonly Color[] RankColors =
        {
            new(0.98f, 0.78f, 0.88f), new(0.72f, 0.88f, 0.98f), new(0.78f, 0.94f, 0.82f), new(0.99f, 0.95f, 0.72f),
            new(0.82f, 0.78f, 0.97f), new(0.99f, 0.84f, 0.70f), new(0.80f, 0.91f, 0.80f),
        };

        static Vector2 P(float x, float y) => Units.P(x, y);

        public LeaderboardScreen(AppRoot app) : base(app, "LeaderboardScreen", themedBackground: false)
        {
            App.SetBackground(Cream);
            SetupBackground();
            SetupUI();
        }

        void SetupBackground()
        {
            ShapeView.Rect("Backdrop", Root, Vector2.zero, new Vector2(SceneSize.x + 200, SceneSize.y + 200), 0, Cream, default, 0, -100);
            var configs = new (float radius, float x, float y, int color, float duration)[]
            {
                (88, -280, 480, 0, 7.2f), (62, 260, 350, 4, 9.5f), (110, -180, -150, 6, 11.0f),
                (74, 310, -320, 1, 8.3f), (96, -60, 560, 3, 10.1f), (54, 200, -500, 7, 6.8f),
                (80, -320, -480, 2, 12.4f), (68, 80, 200, 5, 9.0f), (50, -200, -30, 8, 7.6f),
            };
            foreach (var c in configs)
            {
                var bubble = ShapeView.Circle("Bubble", Root, P(c.x, c.y), c.radius, BubbleColors[c.color].WithAlpha(0.18f), default, 0, -90);
                var t = bubble.transform;
                Tw.Run(bubble.gameObject, Tw.Forever(Tw.Seq(
                    new MoveByTween(t, P(0, 18), c.duration, TwEase.EaseInEaseOut),
                    new MoveByTween(t, P(0, -18), c.duration, TwEase.EaseInEaseOut))), "float");
            }
        }

        void SetupUI()
        {
            float topY = SceneSize.y / 2;
            float bottomY = -HalfH + Geo.SafeBottomPx / Geo.PixelsPerScene;

            TenGoText.Create(Root, Loc.T("leaderboard.title"), Fonts.AvenirHeavy, 44, Colors.White(0.28f), P(0, topY - 110), Z)
                .ShrinkToFit(UsableW - 40);

            var scores = S.Games.HighScores;
            if (scores.Count == 0)
            {
                ProgressUi.Sparkle(Root, new Vector2(0, 60), 36, Colors.White(0.65f), Z);
                TenGoText.Create(Root, Loc.T("leaderboard.empty_title"), Fonts.AvenirMedium, 22, Colors.White(0.32f), P(0, 0), Z)
                    .ShrinkToFit(UsableW - 40);
                TenGoText.Create(Root, Loc.T("leaderboard.empty_subtitle"), Fonts.AvenirMedium, 17, Colors.White(0.55f), P(0, -34), Z)
                    .ShrinkToFit(UsableW - 40);
            }
            else
            {
                AddTopScore(scores[0], topY - 230);
                int restCount = Mathf.Min(6, scores.Count - 1);
                for (int i = 0; i < restCount; i++) AddScorePill(scores[i + 1], i, topY - 410 - i * (52 + 14));
            }

            var back = ProgressUi.BackButton(Root, bottomY + 90, Z + 10);
            AddTarget("back", back, new Vector2(80, 80), () => App.ShowMenu());

            // iOS ne les montre qu'une fois Game Center connecté : s'il ne l'est
            // pas encore, ils apparaissent dès que la connexion aboutit.
            _buttonsY = bottomY;
            if (S.Play.ShowsLeaderboardButtons) AddLeaderboardButtons();
            else S.Play.SignInChanged += OnSignInChanged;
        }

        float _buttonsY;
        bool _buttonsShown;

        /// Boutons de classement affichés (connexion Game Center / Play Games faite).
        public bool LeaderboardButtonsShown => _buttonsShown;

        void OnSignInChanged()
        {
            if (S.Play.ShowsLeaderboardButtons) AddLeaderboardButtons();
        }

        void AddLeaderboardButtons()
        {
            if (_buttonsShown) return;
            _buttonsShown = true;
            float bottomY = _buttonsY;
            AddLeaderboardButton("dailyLeaderboard", Loc.T("leaderboard.daily_button"), new Color(0.86f, 0.82f, 0.97f),
                new Vector2(-100, bottomY + 190), Leaderboard.Daily);
            AddLeaderboardButton("worldLeaderboard", Loc.T("leaderboard.world_button"), new Color(0.72f, 0.88f, 0.98f),
                new Vector2(100, bottomY + 190), Leaderboard.Normal);
            AddLeaderboardButton("rushLeaderboard", Loc.T("leaderboard.rush_button"), new Color(0.99f, 0.84f, 0.70f),
                new Vector2(0, bottomY + 250), Leaderboard.Rush);
        }

        public override void Dispose()
        {
            S.Play.SignInChanged -= OnSignInChanged;
            base.Dispose();
        }

        void AddTopScore(int score, float y)
        {
            const float radius = 70;
            ShapeView.Circle("Halo", Root, P(0, y), radius + 6, RankColors[0].WithAlpha(0.55f), default, 0, Z);
            ShapeView.Circle("Top", Root, P(0, y), radius, new Color(0.96f, 0.93f, 0.90f), Colors.White(0.70f, 0.50f), 1.5f, Z + 1);
            ShapeView.Circle("Dot", Root, P(0, y + radius - 12), 10, BubbleColors[6], default, 0, Z + 3);
            TenGoText.Create(Root, Loc.T("game.points_label"), Fonts.AvenirMedium, 14, Colors.White(0.58f), P(0, y + 20), Z + 3)
                .ShrinkToFit(radius * 1.6f);
            TenGoText.Create(Root, score.ToString(), Fonts.AvenirHeavy, 42, Colors.White(0.28f), P(0, y - 12), Z + 3)
                .ShrinkToFit(radius * 1.8f);
        }

        void AddScorePill(int score, int index, float y)
        {
            const float pillW = 300, pillH = 52;
            var pill = RenderShared.Node("Rank" + (index + 2), Root, P(0, y));
            var bg = ShapeView.Rect("Bg", pill, Vector2.zero, new Vector2(pillW, pillH), pillH / 2,
                RankColors[Mathf.Min(index + 1, RankColors.Length - 1)], Colors.White(0.68f, 0.35f), 1, Z);
            Relief.Raise(bg, 3);
            ProgressUi.Left(pill, "#" + (index + 2), Fonts.AvenirMedium, 15, Colors.White(0.60f), -pillW / 2 + 24, 0, Z + 2);
            ProgressUi.Right(pill, score + " " + Loc.T("game.points_label"), Fonts.AvenirMedium, 20, Colors.White(0.30f),
                pillW / 2 - 24, 0, Z + 2, pillW - 100);
        }

        void AddLeaderboardButton(string name, string title, Color color, Vector2 pos, Leaderboard board)
        {
            const float pillW = 165, pillH = 52;
            var btn = RenderShared.Node(name, Root, P(pos.x, pos.y));
            var bg = ShapeView.Rect("Bg", btn, Vector2.zero, new Vector2(pillW, pillH), pillH / 2, color, Colors.White(0.60f, 0.35f), 1, Z);
            Relief.Raise(bg, 5);
            TenGoText.Create(btn, title, Fonts.AvenirMedium, 17, Colors.White(0.25f), Vector2.zero, Z + 2).ShrinkToFit(pillW - 20);
            AddTarget(name, btn, new Vector2(pillW, pillH), () => S.Play.ShowLeaderboard(board));
        }

        public override void Back() => App.ShowMenu();
    }
}
