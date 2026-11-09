using System.Collections.Generic;
using TenGo.App;
using TenGo.Core;
using TenGo.Fx;
using TenGo.Rendering;
using TenGo.Services;
using TenGo.Ui;
using TMPro;
using UnityEngine;

namespace TenGo.Screens
{
    /// Classement (LeaderboardScene.swift), refait pour la 4.0
    /// (`design/refonte/06_autres`) : retour en haut à gauche, contrôle
    /// segmenté Mondial / Défi du jour / Rush, carte « ton meilleur », liste
    /// des meilleures parties locales (la première surlignée), puis le
    /// classement en ligne en bouton secondaire. Le rang mondial et la date
    /// des parties ne sont pas connus localement : ils ne sont pas affichés.
    ///
    /// Cibles : « board_world|daily|rush » (segments), « worldLeaderboard »,
    /// « dailyLeaderboard », « rushLeaderboard » (ouvre le classement en
    /// ligne du segment, une fois Game Center / Play Jeux connecté), « back ».
    public sealed class LeaderboardScreen : ScreenBase
    {
        const int Z = 20;
        static readonly Leaderboard[] Boards = { Leaderboard.Normal, Leaderboard.Daily, Leaderboard.Rush };
        static readonly string[] BoardIds = { "world", "daily", "rush" };
        static readonly string[] OnlineTargets = { "worldLeaderboard", "dailyLeaderboard", "rushLeaderboard" };

        Transform _ui;
        int _board;
        bool _buttonsShown;
        bool _listening;

        static float Pt(float v) => Design.Pt(v);

        /// Segment affiché (0 mondial, 1 défi du jour, 2 Rush).
        public int Board => _board;
        /// Bouton du classement en ligne affiché (connexion Game Center / Play Games faite).
        public bool LeaderboardButtonsShown => _buttonsShown;

        public LeaderboardScreen(AppRoot app) : base(app, "LeaderboardScreen")
        {
            TapOnRelease = true;
            Rebuild();
        }

        public void SelectBoard(int index)
        {
            if (index == _board || index < 0 || index >= Boards.Length) return;
            _board = index;
            Rebuild();
        }

        void Rebuild()
        {
            if (_ui != null) RenderShared.Destroy(_ui.gameObject);
            Targets.Clear();
            _ui = RenderShared.Node("LeaderboardUI", Root);
            _buttonsShown = false;
            SetupUI();
        }

        void SetupUI()
        {
            float cw = ContentW, left = -cw / 2;
            AddBackHeader(Loc.T("leaderboard.title"), null, () => App.ShowMenu(), _ui);

            float cursor = HeaderBottom - Pt(10);
            var labels = new[] { Loc.T("ui.leaderboard.world"), Loc.T("leaderboard.daily_button"), Loc.T("leaderboard.rush_button") };
            var segs = Design.Segmented(_ui, new Vector2(0, cursor - Pt(20)), cw, labels, _board, Z);
            float segW = (cw - Pt(8)) / labels.Length;
            for (int i = 0; i < segs.Length; i++)
            {
                int b = i;
                segs[i].name = "board_" + BoardIds[i];
                AddTarget(segs[i].name, segs[i], new Vector2(segW, Pt(40)), () => SelectBoard(b), animate: false);
            }
            cursor -= Pt(40 + 14);

            // Carte « ton meilleur ».
            int best = _board switch { 0 => S.Games.BestScore, 2 => S.Games.RushBest, _ => S.Streak.Current };
            string caption = _board == 1 ? Loc.T("profile.daily_streak") : Loc.T("ui.leaderboard.your_best");
            float meH = Pt(84);
            var me = RenderShared.Node("me", _ui, Units.P(0, cursor - meH / 2));
            Design.Surface("Bg", me, Vector2.zero, new Vector2(cw, meH), Pt(Design.CardRadius), Z);
            var icon = _board == 1 ? VectorIconName.LineFlame : VectorIconName.LineTrophy;
            Design.IconChip(me, new Vector2(left + Pt(16 + 26), 0), 52, 17, Look.BubbleColor(3), icon, Z + 1, Design.Ink, 24, 0.5f);
            float tx = left + Pt(16 + 52 + 14);
            Design.Text(me, Design.Number(best), NunitoWeight.Black, 26, Design.Ink, new Vector2(tx, Pt(10)), Z + 2, HorizontalAlignmentOptions.Left);
            Design.Text(me, caption, NunitoWeight.Bold, 13, Design.Ink2, new Vector2(tx, -Pt(16)), Z + 2, HorizontalAlignmentOptions.Left)
                .ShrinkToFit(-left - Pt(16) - tx);
            cursor -= meH + Pt(22);

            // Meilleures parties locales (Normal et Rush).
            IReadOnlyList<int> scores = _board switch { 0 => S.Games.HighScores, 2 => S.Games.RushHighScores, _ => null };
            if (scores != null)
            {
                Design.Text(_ui, Loc.T("ui.leaderboard.best_games"), NunitoWeight.Black, 17, Design.Ink, new Vector2(left, cursor), Z + 2,
                    HorizontalAlignmentOptions.Left);
                cursor -= Pt(10 + 12);
                if (scores.Count == 0)
                {
                    Design.Text(_ui, Loc.T("leaderboard.empty_title"), NunitoWeight.ExtraBold, 16, Design.Ink, new Vector2(0, cursor - Pt(20)), Z + 2)
                        .ShrinkToFit(cw);
                    Design.Text(_ui, Loc.T("leaderboard.empty_subtitle"), NunitoWeight.Bold, 13, Design.Ink2, new Vector2(0, cursor - Pt(44)), Z + 2)
                        .ShrinkToFit(cw);
                }
                else
                {
                    float rowH = Pt(48);
                    float bottomLimit = -HalfH + BottomInset + Pt(12 + 56 + 16);
                    int n = Mathf.Min(scores.Count, Mathf.Max(1, (int)((cursor - bottomLimit) / rowH)), 7);
                    float cardH = rowH * n;
                    var card = RenderShared.Node("Ranks", _ui, Units.P(0, cursor - cardH / 2));
                    Design.Surface("Bg", card, Vector2.zero, new Vector2(cw, cardH), Pt(Design.CardRadius), Z);
                    for (int i = 0; i < n; i++)
                    {
                        float y = cardH / 2 - rowH * (i + 0.5f);
                        if (i == 0)
                        {
                            // Première ligne surlignée (coins du haut arrondis).
                            var hl = Design.Tint(Design.Accent, 0.12f);
                            ShapeView.Rect("You", card, Units.P(0, y), new Vector2(cw, rowH), Pt(Design.CardRadius), hl, default, 0, Z + 1);
                            if (n > 1) ShapeView.Rect("YouFill", card, Units.P(0, y - rowH / 4), new Vector2(cw, rowH / 2), 0, hl, default, 0, Z + 1);
                        }
                        else ShapeView.Rect("Sep", card, Units.P(0, y + rowH / 2), new Vector2(cw, Pt(1)), 0, Design.Line, default, 0, Z + 1);
                        Design.Text(card, (i + 1).ToString(), NunitoWeight.Black, 14, Design.Ink3, new Vector2(left + Pt(16), y), Z + 2,
                            HorizontalAlignmentOptions.Left);
                        Design.Text(card, Loc.F("ui.leaderboard.game_n", i + 1), NunitoWeight.ExtraBold, 15, Design.Ink, new Vector2(left + Pt(52), y), Z + 2,
                            HorizontalAlignmentOptions.Left);
                        Design.Text(card, Design.Number(scores[i]), NunitoWeight.Black, 15, Design.Ink, new Vector2(-left - Pt(16), y), Z + 2,
                            HorizontalAlignmentOptions.Right);
                    }
                }
            }

            // iOS ne les montre qu'une fois Game Center connecté : s'il ne l'est
            // pas encore, le bouton apparaît dès que la connexion aboutit.
            if (S.Play.ShowsLeaderboardButtons) AddOnlineButton();
            else if (!_listening) { S.Play.SignInChanged += OnSignInChanged; _listening = true; }
        }

        void OnSignInChanged()
        {
            if (S.Play.ShowsLeaderboardButtons) AddOnlineButton();
        }

        /// « Voir dans Game Center / Google Play Games » : le classement en ligne du segment.
        void AddOnlineButton()
        {
            if (_buttonsShown || _ui == null) return;
            _buttonsShown = true;
            float cw = ContentW;
            string service = Application.platform == RuntimePlatform.Android ? "Google Play Games" : "Game Center";
            float y = -HalfH + BottomInset + Pt(12 + 28);
            string name = OnlineTargets[_board];
            var board = Boards[_board];
            var btn = Design.Button(name, _ui, new Vector2(0, y), cw, Loc.F("ui.leaderboard.open", service), false, Z, VectorIconName.LineTrophy);
            AddTarget(name, btn, new Vector2(cw, Pt(56)), () => S.Play.ShowLeaderboard(board));
        }

        public override void Dispose()
        {
            if (_listening) S.Play.SignInChanged -= OnSignInChanged;
            base.Dispose();
        }

        public override void Back() => App.ShowMenu();
    }
}
