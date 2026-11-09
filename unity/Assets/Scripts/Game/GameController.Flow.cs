using System.Collections.Generic;
using TenGo.Audio;
using TenGo.Core;
using TenGo.Fx;
using TenGo.Haptics;
using TenGo.Platform;
using TenGo.Rendering;
using UnityEngine;

namespace TenGo.Game
{
    // MARK: - Relance, démo, navigation, crochets de QA
    public sealed partial class GameController
    {
        bool _didMove;

        void ResetGame()
        {
            // Garde d'iOS reprise telle quelle : seul un message affiché pendant une animation bloque.
            if (_isAnimating && _messageLabel.gameObject.activeSelf) return;
            PerformReset();
        }

        void PerformReset()
        {
            IsAnimating = true;
            _messageLabel.gameObject.SetActive(false);
            _app.SetBackground(Look.Background);
            _score = 0;
            _displayedScore = 0;
            _combosCreated = 0;
            _longestChain = 0;
            _celebratedChain = 0;
            _isWinState = false;
            _scoreLabel.Text = "0";
            _app.Shake?.ResetView();
            _juice.ResetTimeScale();
            CancelPath();
            DismissNoAdsOffer();

            Kill(_gameOverOverlay);
            _gameOverOverlay = null;
            if (_gameOverPanel != null)
            {
                var panel = _gameOverPanel;
                panel.name = "GameOverPanelClosing";
                Tw.Run(panel.gameObject, Tw.Seq(Tw.FadeOut(panel.gameObject, 0.18f), Tw.Remove(panel.gameObject)));
                _gameOverPanel = null;
            }
            _zones.Clear();
            FadeOutAllBubbles(0.2f);

            After(0.25f, () =>
            {
                if (Kind == GameModeKind.Daily && _dailyToday != null) _grid = _dailyToday.Grid;
                else if (Kind == GameModeKind.Puzzle && _puzzleLevel != null) _grid = _puzzleLevel.MakeGrid();
                else if (Kind == GameModeKind.Rush) _grid = GridModel.Generate(_rushRng);
                else
                {
                    _grid = GridModel.Generate(SystemRng.Create());
                    PlantBloomedFlowerIfAny();
                }
                SetupGridAnimated();
                _lastResult = null;
                _endRecorded = false;
                _secondChanceUsed = false;
                _secondChanceGrid = null;
                _continuedFrom = null;
                _hintVideosThisGame = 0;
                ResetMirrorTracking();
                _lastPlayerActionAt = FxTime.RealNow;
                _hammerArmed = false;
                if (_boosterBar != null) _boosterBar.gameObject.SetActive(true);
                RefreshBoosterButtons();
                if (Kind == GameModeKind.Rush)
                {
                    _rushTimerRunning = false;
                    _rushAwaitingBoard = false;
                    _rushEnded = false;
                    _rushTimeRemaining = RushDuration;
                    UpdateRushTimerLabel();
                    StartRushCountdown();
                }
            });
        }

        void FadeOutAllBubbles(float duration)
        {
            for (int r = 0; r < GameRules.Rows; r++)
            for (int c = 0; c < GameRules.Cols; c++)
            {
                if (_nodes[r, c] is BubbleView n)
                {
                    FxAlpha.Of(n.gameObject);
                    Tw.Run(n.gameObject, Tw.Seq(Tw.FadeOut(n.gameObject, duration), Tw.Remove(n.gameObject)), "fadeOut");
                }
                _nodes[r, c] = null;
            }
        }

        /// Apparition en cascade (décalage 0,012 s) ; rend la main à la fin.
        void SetupGridAnimated()
        {
            RenderObstacles();
            float delay = 0;
            const float stagger = 0.012f;
            for (int r = 0; r < GameRules.Rows; r++)
            for (int c = 0; c < GameRules.Cols; c++)
            {
                var m = _grid.Cells[r][c];
                if (m == null) continue;
                var node = MakeBubble(m, ScenePos(r, c));
                node.transform.localScale = Vector3.zero;
                FxAlpha.Of(node.gameObject).Alpha = 0;
                _nodes[r, c] = node;
                Tw.Run(node.gameObject, Tw.Seq(Tw.Wait(delay), Tw.Par(Tw.Scale(node.transform, 1f, 0.15f), Tw.FadeIn(node.gameObject, 0.15f))), "appear");
                delay += stagger;
            }
            After(delay + 0.15f, () => IsAnimating = false);
        }

        // MARK: - Démo (auto-player)

        void StartDemo() => After(0.6f, AutoPlayNext, "demoLoop");

        void AutoPlayNext()
        {
            if (Kind != GameModeKind.Demo) return;
            if (_isAnimating) { After(0.04f, AutoPlayNext, "demoLoop"); return; }
            if (_demoScript != null)
            {
                if (_demoScriptIndex >= _demoScript.Count) return;
                AnimateDemoPath(_demoScript[_demoScriptIndex++]);
                return;
            }
            var path = _grid.ShowcasePath(_demoMaxLen);
            if (path == null || path.Count < 2) { DemoReseed(); return; }
            AnimateDemoPath(path);
        }

        void AnimateDemoPath(List<Cell> path)
        {
            float step = (float)(0.085 / _demoSpeed);
            float pause = (float)(0.14 / _demoSpeed);
            var seq = new List<Tween> { Tw.Call(() => DemoBeginPath(path[0])) };
            for (int i = 1; i < path.Count; i++)
            {
                var c = path[i];
                seq.Add(Tw.Wait(step));
                seq.Add(Tw.Call(() => TryAppendCell(c)));
            }
            seq.Add(Tw.Wait(pause));
            seq.Add(Tw.Call(() =>
            {
                if (_grid.PathSum(_path) == GameRules.TargetSum) { HapticManager.Medium(); CommitPath(); }
                else CancelPath();
            }));
            seq.Add(Tw.Call(AutoPlayNext));
            Tw.Run(Root.gameObject, Tw.Seq(seq.ToArray()), "demoLoop");
        }

        void DemoBeginPath(Cell coord)
        {
            _path.Clear();
            _path.Add(coord);
            _nodes[coord.Row, coord.Col]?.SetSelected(true);
            SoundManager.Shared.PlaySelect(_grid.Cells[coord.Row][coord.Col]?.Value ?? 1);
            UpdatePathLine();
        }

        /// Grille suivante (graine + manche) : le score continue de grimper.
        void DemoReseed()
        {
            if (Kind != GameModeKind.Demo || _demoScript != null) return;
            IsAnimating = true;
            _demoRound++;
            FadeOutAllBubbles((float)(0.18 / _demoSpeed));
            Tw.Run(Root.gameObject, Tw.Seq(
                Tw.Wait((float)(0.24 / _demoSpeed)),
                Tw.Call(() =>
                {
                    _grid = GridModel.Generate(new SeededGenerator(unchecked(_demoSeed + (ulong)_demoRound)));
                    SetupGridAnimated();
                }),
                Tw.Wait((float)(0.2 / _demoSpeed)),
                Tw.Call(AutoPlayNext)), "demoLoop");
        }

        // MARK: - Navigation

        /// Boutique depuis la partie, qui est préservée (sans interstitielle).
        void NavigateToBoutique(string reason)
        {
            _boosterCoach?.Dismiss();
            _boosterCoach = null;
            DisarmHammer();
            CancelPath();
            DismissBoosterShopPrompt();
            bool returnToGame = _hasPendingShopSnapshot && _pendingShopGrid != null;
            if (returnToGame) _s.Games.Save(_pendingShopGrid, _pendingShopScore);
            else if (_hasPendingShopSnapshot) { _s.Games.Save(_grid, _score); returnToGame = true; }
            _s.Analytics.ShopOpenedFromGame(reason);
            _hasPendingShopSnapshot = false;
            _pendingShopGrid = null;
            _app.FlushSave();
            _app.Router.ShowShop(returnToGame);
        }

        void GoBackToMenu()
        {
            _boosterCoach?.Dismiss();
            _boosterCoach = null;
            // Le défi du jour ne se sauvegarde pas dans l'emplacement « Continuer ».
            if (Kind == GameModeKind.Normal && !_grid.IsGridEmpty() && !_isAnimating && _gameOverPanel == null)
                _s.Games.Save(_grid, _score);
            _app.FlushSave();
            _s.Interstitials.MaybeShow(AdTrigger.Home, _ =>
            {
                if (_disposed) return;
                switch (Kind)
                {
                    case GameModeKind.Puzzle: _app.Router.ShowPuzzleLevels(_puzzleLevel?.World ?? 1); break;
                    case GameModeKind.Duel: _app.Router.ShowDuel(); break;
                    default: _app.Router.ShowMenu(); break;
                }
            });
        }

        // MARK: - Crochets de QA et de capture (équivalents des variables d'environnement iOS)

        void ApplyQaHooks() { }

        /// `SCREENSHOT_END` : fin de partie avec un score de vitrine, sans seconde chance.
        public void QaForceGameOver(int score, int longestChain, int combos, bool allowSecondChance = false)
        {
            if (_gameOverPanel != null) return;
            CancelPath();
            _score = score;
            _displayedScore = score;
            _scoreLabel.Text = score.ToString();
            _longestChain = longestChain;
            _combosCreated = combos;
            IsAnimating = true;
            if (allowSecondChance) PrepareSecondChance();
            TriggerLose();
        }

        /// `SCREENSHOT_PATH=N` : trace sans le valider le plus long chemin de N bulles au plus.
        public bool QaShowcasePath(int maxLen, int? score = null, int? flowerPower = null)
        {
            var path = _grid.ShowcasePath(maxLen);
            if (path == null || path.Count < 2) return false;
            if (score is int s) { _score = s; _displayedScore = s; _scoreLabel.Text = s.ToString(); }
            if (flowerPower is int p)
            {
                var mid = path[path.Count / 2];
                _grid.SetBloom(p, mid);
                _nodes[mid.Row, mid.Col]?.SetBloom(p);
            }
            DemoBeginPath(path[0]);
            for (int i = 1; i < path.Count; i++) TryAppendCell(path[i]);
            return true;
        }

        /// Fin de Rush immédiate (tests, pilote) : comme si le chrono tombait à zéro.
        public void QaEndRush()
        {
            if (Kind != GameModeKind.Rush) return;
            if (_rushCountdownActive) { Tw.Kill(Root.gameObject, "rushCountdown"); if (_rushCountdownLabel != null) Kill(_rushCountdownLabel.gameObject); StartRushTimer(); }
            _rushTimeRemaining = 0;
            TriggerRushTimeUp();
        }

        /// Point de scène du centre d'une case (pilote automatique, tests).
        public static Vector2 CellCenter(Cell c) => ScenePt(c.Row, c.Col);
        /// Points de scène des boutons (pilote, tests).
        public Vector2? BoosterPoint(Booster b)
        {
            foreach (var (bb, node) in _boosterButtons) if (bb == b) return WorldPt(node);
            return null;
        }
        public Vector2 RestartPoint => WorldPt(_restartBubbleNode);
        public Vector2 HomePoint => WorldPt(_homeBubbleNode);
        public Vector2? PanelButtonPoint(string name)
        {
            var n = FindNamed(_gameOverPanel, name);
            return n != null ? WorldPt(n) : (Vector2?)null;
        }
        public Vector2? NoAdsDeclinePoint => _noAdsOffer != null ? WorldPt(_noAdsOffer) + NoAdsDeclineRect.center : (Vector2?)null;
        public bool NoAdsOfferVisible => _noAdsOffer != null;
    }
}
