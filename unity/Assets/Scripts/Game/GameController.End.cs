using TenGo.Audio;
using TenGo.Core;
using TenGo.Fx;
using TenGo.Rendering;
using TenGo.Services;
using UnityEngine;

namespace TenGo.Game
{
    // MARK: - Victoire, défaite, enregistrement, Rush
    public sealed partial class GameController
    {
        /// Bilan de la première fin de partie, quand « Continuer » a relancé une partie déjà comptée.
        GameEndResult _continuedFrom;
        /// Le bilan de la fin de partie en cours est déjà passé par le coordinateur.
        bool _endRecorded;

        /// Enregistre la fin de partie (`recordScore`) : une seule fois par fin.
        void RecordScore()
        {
            if (_endRecorded) return;
            _endRecorded = true;
            _gameEndsRecorded++;

            var puzzle = Kind == GameModeKind.Puzzle ? _puzzleLevel : Kind == GameModeKind.Demo ? _demoPuzzle : null;
            int dayKey = _dailyToday?.DayKey ?? 0;
            var input = new GameEndInput(Mode, _score, _isWinState, _combosCreated, _longestChain, dayKey, puzzle,
                Kind == GameModeKind.Demo ? null : _mirrorTally, Kind == GameModeKind.Normal ? _continuedFrom : null);
            _lastResult = _s.Coordinator.OnGameEnded(input);

            switch (Kind)
            {
                case GameModeKind.Normal: _s.Play.SubmitScore(Leaderboard.Normal, _score); break;
                case GameModeKind.Daily: _s.Play.SubmitScore(Leaderboard.Daily, _score); break;
                case GameModeKind.Rush: _s.Play.SubmitScore(Leaderboard.Rush, _score); break;
                case GameModeKind.Duel: PublishDuelResult(); break;
            }
            if (Kind != GameModeKind.Demo) AnnounceAchievements(_lastResult.NewAchievements);
            _app.FlushSave();
        }

        // MARK: Duel

        /// Publie le résultat : création du duel (challenger) ou ajout du score (adversaire).
        void PublishDuelResult()
        {
            int finalScore = _score;
            void Done(Duel duel)
            {
                if (_disposed) return;
                if (_duelCode == null) _duelCode = duel.Code;
                UpdateDuelStatus(duel);
            }
            void Fail(string message) { if (!_disposed && _duelStatusLabel != null) _duelStatusLabel.Text = message; }
            if (_duelCode != null) _s.Duels.SubmitOpponentScore(_duelCode, finalScore, Done, Fail);
            else _s.Duels.CreateDuel(_duelSeed, finalScore, Done, Fail);
        }

        Duel _pendingDuel;

        void UpdateDuelStatus(Duel duel)
        {
            if (_gameOverPanel == null) { _pendingDuel = duel; return; }
            string uid = _s.Duels.Uid;
            if (uid == null) return;
            if (duel.OutcomeFor(uid) is DuelOutcome outcome)
            {
                string key = outcome switch { DuelOutcome.Win => "duel.result_win", DuelOutcome.Loss => "duel.result_loss", _ => "duel.result_draw" };
                int theirs = uid == duel.ChallengerUid ? (duel.OpponentScore ?? 0) : duel.ChallengerScore;
                SetDuelStatus(Loc.F(key, duel.RivalName(uid), theirs));
            }
            else
            {
                // Le challenger repart avec le code, et de quoi l'envoyer.
                SetDuelStatus(Loc.F("duel.share_code", duel.Code));
                AddDuelShareButton(duel.Code);
            }
        }

        void SetDuelStatus(string text)
        {
            if (_duelStatusLabel == null) return;
            _duelStatusLabel.Text = text;
            _duelStatusLabel.ShrinkToFit(440);
        }

        void AddDuelShareButton(string code)
        {
            if (_gameOverPanel == null || FindNamed(_gameOverPanel, "duelShareBtn") != null) return;
            var fill = Look.BubbleColor(4);
            var ink = fill.ReadableInk();
            var button = RenderShared.Node("duelShareBtn", _gameOverPanel, P(0, -184));
            FxAlpha.Of(button.gameObject);
            Relief.Raise(ShapeView.Rect("Bg", button, Vector2.zero, new Vector2(320, 68), 34, fill, Colors.White(0.68f, 0.30f), 1, PanelZ + 2), 5);
            var label = Label(button, Loc.T("duel.share_button"), Fonts.AvenirDemiBold, 22, ink, Vector2.zero, PanelZ + 5, TMPro.HorizontalAlignmentOptions.Left);
            const float iconSize = 24, inner = 12;
            float contentW = iconSize + inner + label.WidthPt();
            VectorIcon.Create(button, VectorIconName.IconShare, P(-contentW / 2 + iconSize / 2, 0), iconSize, ink, PanelZ + 5);
            label.transform.localPosition = P(-contentW / 2 + iconSize + inner, 0);
            AddZone("duelShareBtn", button, new Vector2(320, 68));
            _duelShareCode = code;
            button.localScale = Vector3.one * 0.85f;
            FxAlpha.Of(button.gameObject).Alpha = 0;
            Tw.Run(button.gameObject, Tw.Par(Tw.FadeIn(button.gameObject, 0.2f), Tw.Scale(button, 1f, 0.26f)));
        }

        // MARK: Victoire / défaite

        void TriggerWin()
        {
            _juice.ResetTimeScale();
            SoundManager.Shared.PlayWin();
            if (Kind != GameModeKind.Demo) _s.Interstitials.MarkGameCompleted();
            _isWinState = true;
            // Pas de bonus en puzzle : il fausserait les seuils d'étoiles.
            int bonus = Kind == GameModeKind.Puzzle || _demoPuzzle != null ? 0 : GameRules.WinBonus;
            SyncDisplayedScore();
            _score += bonus;
            if (bonus > 0)
            {
                _s.Coordinator.OnBonusScored(Mode, bonus);
                ShowScorePopup(bonus, new Vector2(0, 50), PopTier.Large);
            }
            _juice.OnWin(Vector2.zero, Look.Accent);
            RecordScore();
            After(1.1f, ShowGameOverPanel);
            After(2.6f, MaybeRequestReview);
        }

        /// Demande d'avis après une victoire, pour un joueur engagé, jamais à la toute première partie.
        void MaybeRequestReview()
        {
            if (Kind == GameModeKind.Demo || !_s.Settings.HasSeenTutorial) return;
            if (_lastResult != null && _lastResult.WasFirstGame) return;
            _s.Play.RequestReview();
        }

        void TriggerLose()
        {
            SoundManager.Shared.PlayLose();
            if (Kind != GameModeKind.Demo) _s.Interstitials.MarkGameCompleted();
            _isWinState = false;
            _juice.ResetTimeScale();
            _juice.OnLose();

            // Défaite adoucie : les bulles s'affaissent en cascade.
            for (int r = 0; r < GameRules.Rows; r++)
            for (int c = 0; c < GameRules.Cols; c++)
            {
                var node = _nodes[r, c];
                if (node == null) continue;
                node.CancelNudge();
                if (!JuiceSettings.MotionEnabled) continue;
                Tw.Run(node.gameObject, Tw.Seq(Tw.Wait(r * 0.02f), Tw.MoveBy(node.transform, P(0, -6), 0.22f).Ease(TwEase.EaseOut)), "sag");
            }
            Tw.Run(Root.gameObject, Tw.Seq(
                Tw.Wait(0.35f),
                Tw.Call(() =>
                {
                    if (_disposed) return;
                    for (int r = 0; r < GameRules.Rows; r++)
                    for (int c = 0; c < GameRules.Cols; c++)
                        if (_nodes[r, c] is BubbleView n) Tw.Run(n.gameObject, Tw.Fade(n.gameObject, 0.12f, 0.4f), "dim");
                    RecordScore();
                }),
                Tw.Wait(0.5f),
                Tw.Call(() => { if (!_disposed) ShowGameOverPanel(); })));
        }

        // MARK: - Rush

        double _rushLastUpdate;
        TenGoText _rushCountdownLabel;

        /// « PRÊT… 3, 2, 1, GO ! » : l'entrée est bloquée par `_rushCountdownActive` (jamais par `IsAnimating`).
        void StartRushCountdown()
        {
            _rushCountdownActive = true;
            var label = Label(Root, "", Fonts.AvenirHeavy, 64, Look.Logo, Vector2.zero, 200);
            _rushCountdownLabel = label;
            string[] steps = { Loc.T("rush.ready"), "3", "2", "1", Loc.T("rush.go") };
            var seq = new System.Collections.Generic.List<Tween>();
            foreach (var step in steps)
            {
                string s = step;
                seq.Add(Tw.Call(() =>
                {
                    if (label == null) return;
                    label.Text = s;
                    label.transform.localScale = Vector3.one * 0.7f;
                    Tw.Run(label.gameObject, Tw.Scale(label.transform, 1f, 0.18f), "pop");
                }));
                seq.Add(Tw.Wait(0.6f));
            }
            seq.Add(Tw.Call(() =>
            {
                if (label != null) Kill(label.gameObject);
                _rushCountdownLabel = null;
                if (!_disposed) StartRushTimer();
            }));
            Tw.Run(Root.gameObject, Tw.Seq(seq.ToArray()), "rushCountdown");
        }

        void StartRushTimer()
        {
            _rushCountdownActive = false;
            _boardReadyAt = FxTime.RealNow;
            _rushTimeRemaining = RushDuration;
            _rushLastUpdate = 0;
            _rushTimerRunning = true;
            UpdateRushTimerLabel();
        }

        /// Chrono en temps RÉEL (hors actions) : l'arrêt sur image ne doit pas l'allonger.
        void UpdateRush(float dt)
        {
            if (Kind != GameModeKind.Rush || !_rushTimerRunning || _rushEnded) return;
            double now = FxTime.RealNow;
            if (_rushAwaitingBoard)
            {
                if (_isAnimating) { _rushLastUpdate = 0; return; }
                _rushAwaitingBoard = false;
            }
            double last = _rushLastUpdate;
            _rushLastUpdate = now;
            if (last <= 0) return;
            double delta = now - last;
            // Retour d'arrière-plan : un delta énorme est ignoré.
            if (delta <= 0 || delta >= 1.0) return;
            _rushTimeRemaining -= delta;
            UpdateRushTimerLabel();
            if (_rushTimeRemaining <= 0) TriggerRushTimeUp();
        }

        void UpdateRushTimerLabel()
        {
            if (_rushTimerLabel != null) _rushTimerLabel.Text = Mathf.Max(0, (int)System.Math.Ceiling(_rushTimeRemaining)).ToString();
        }

        void AwardRushTimeBonus()
        {
            _rushTimeRemaining += RushBonusSeconds;
            UpdateRushTimerLabel();
            if (_rushTimerLabel == null) return;
            var pos = (Vector2)_rushTimerLabel.transform.localPosition + P(46, 0);
            var bonus = Label(Root, "+" + (int)RushBonusSeconds + "s", Fonts.AvenirBold, 20, new Color(0.35f, 0.70f, 0.45f, 1), pos, 200);
            FxAlpha.Of(bonus.gameObject);
            Tw.Run(bonus.gameObject, Tw.Seq(Tw.Par(Tw.MoveBy(bonus.transform, P(0, 24), 0.6f), Tw.FadeOut(bonus.gameObject, 0.6f)), Tw.Remove(bonus.gameObject)));
        }

        /// Grille vidée en Rush : un exploit, pas une fin. Bonus, puis grille neuve.
        void RushBoardCleared()
        {
            _juice.ResetTimeScale();
            SoundManager.Shared.PlayWin();
            int bonus = GameRules.WinBonus;
            SyncDisplayedScore();
            _score += bonus;
            _s.Coordinator.OnBonusScored(Mode, bonus);
            ShowScorePopup(bonus, new Vector2(0, 50), PopTier.Large);
            _juice.OnWin(Vector2.zero, Look.Accent);
            AnnounceAchievements(_s.Coordinator.OnRushBoardCleared());
            RushReseed();
        }

        void RushReseed()
        {
            IsAnimating = true;
            _rushAwaitingBoard = true;
            FadeOutAllBubbles(0.18f);
            After(0.24f, () =>
            {
                _grid = GridModel.Generate(_rushRng);
                SetupGridAnimated();
            });
        }

        /// Fin du Rush : le chrono seul décide.
        void TriggerRushTimeUp()
        {
            if (_rushEnded) return;
            _rushEnded = true;
            _rushTimerRunning = false;
            IsAnimating = true;
            _isWinState = false;
            CancelPath();
            SoundManager.Shared.PlayLose();
            _s.Interstitials.MarkGameCompleted();
            SyncDisplayedScore();
            RecordScore();
            After(0.4f, ShowGameOverPanel);
        }
    }
}
