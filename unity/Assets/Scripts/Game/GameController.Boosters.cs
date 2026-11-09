using System.Collections.Generic;
using TenGo.Core;
using TenGo.Fx;
using TenGo.Haptics;
using TenGo.Platform;
using TenGo.Rendering;
using UnityEngine;

namespace TenGo.Game
{
    // MARK: - Boosters, coach-marks, modale « à court de boosters », vidéos récompensées
    public sealed partial class GameController
    {
        const int BarOrder = 30;
        static readonly Color IconInk = Colors.White(0.42f, 1);

        static VectorIconName IconOf(Booster b) => b switch
        {
            Booster.Hint => VectorIconName.BoosterHint,
            Booster.Shuffle => VectorIconName.BoosterShuffle,
            _ => VectorIconName.BoosterHammer,
        };

        void SetupBoosters()
        {
            if (Kind != GameModeKind.Normal) return;
            _boosterBar = RenderShared.Node("boosterBar", Root);
            RefreshBoosterButtons();
        }

        Transform MakeBoosterButton(Booster booster)
        {
            var node = RenderShared.Node("booster_" + booster.Id(), _boosterBar);
            FxAlpha.Of(node.gameObject);
            Relief.Raise(ShapeView.Circle("Circle", node, Vector2.zero, BoosterRadius, Color.white, default, 0, BarOrder), 4, fill: Color.white);
            VectorIcon.Create(node, IconOf(booster), Vector2.zero, 44, IconInk, BarOrder + 2);
            ShapeView.Circle("Badge", node, P(31, -31), 17, new Color(0.97f, 0.95f, 0.92f, 1), Colors.White(0.70f, 0.5f), 1, BarOrder + 3);
            _boosterCountLabels[booster] = Label(node, "", Fonts.AvenirBold, 14, Colors.White(0.30f, 1), P(31, -31), BarOrder + 4);
            return node;
        }

        /// Seuls les boosters possédés sont affichés ; la barre se recompose.
        void RefreshBoosterButtons()
        {
            if (Kind != GameModeKind.Normal || _boosterBar == null) return;
            var wanted = new List<Booster>();
            foreach (var b in BoosterOrder) if (_s.Economy.BoosterCount(b) > 0) wanted.Add(b);
            var current = new List<Booster>();
            foreach (var (b, _) in _boosterButtons) current.Add(b);

            foreach (var (b, node) in _boosterButtons)
            {
                if (wanted.Contains(b)) continue;
                _boosterCountLabels.Remove(b);
                node.name = "removed";
                Tw.Run(node.gameObject, Tw.Seq(Tw.Par(Tw.FadeOut(node.gameObject, 0.16f), Tw.Scale(node, 0.2f, 0.16f)), Tw.Remove(node.gameObject)));
            }
            _boosterButtons.RemoveAll(x => !wanted.Contains(x.booster));

            foreach (var b in wanted)
            {
                if (current.Contains(b)) continue;
                var node = MakeBoosterButton(b);
                _boosterButtons.Add((b, node));
                AnimateBoosterAppearance(node);
            }
            _boosterButtons.Sort((a, c) => System.Array.IndexOf(BoosterOrder, a.booster).CompareTo(System.Array.IndexOf(BoosterOrder, c.booster)));

            foreach (var (b, _) in _boosterButtons)
                if (_boosterCountLabels.TryGetValue(b, out var l)) l.Text = "×" + _s.Economy.BoosterCount(b);

            SyncShopPill(wanted.Count == 0);

            bool changed = wanted.Count != current.Count;
            for (int i = 0; !changed && i < wanted.Count; i++) changed = wanted[i] != current[i];
            if (changed) RepositionBottomRow(animated: current.Count > 0 && wanted.Count > 0);
            QueueBoosterCoachMarks();
        }

        /// Pastille « Boutique » à la place d'une barre vide, quand elle a du sens.
        void SyncShopPill(bool barIsEmpty)
        {
            bool deserved = barIsEmpty && (_s.Economy.Balance >= Booster.Hint.Price() || _s.Economy.ShopGuideCompleted);
            if (!deserved)
            {
                Kill(_shopPillNode);
                _shopPillNode = null;
                return;
            }
            if (_shopPillNode != null) return;
            var node = RenderShared.Node("boosterShopPill", _boosterBar);
            Relief.Raise(ShapeView.Rect("Pill", node, Vector2.zero, new Vector2(196, 52), 26, Color.white, default, 0, BarOrder), 4, fill: Color.white);
            VectorIcon.Create(node, VectorIconName.BoosterHint, P(-64, 0), 26, IconInk, BarOrder + 2);
            Label(node, Loc.T("game.shop_button"), Fonts.AvenirDemiBold, 18, Colors.White(0.35f, 1), P(12, 0), BarOrder + 2);
            _shopPillNode = node;
        }

        // MARK: Coach-marks

        void QueueBoosterCoachMarks()
        {
            var pending = new List<Booster>();
            foreach (var (b, _) in _boosterButtons) if (!_s.Economy.BoosterCoachSeen(b)) pending.Add(b);
            if (pending.Count == 0) return;
            _boosterCoachQueue.Clear();
            _boosterCoachQueue.AddRange(pending);
            ShowNextBoosterCoach();
        }

        /// Le prochain coach-mark, seulement si le moment s'y prête (pas d'animation, pas de tracé).
        void ShowNextBoosterCoach()
        {
            if (Kind != GameModeKind.Normal || !_didMove || _boosterCoach != null || _gameOverPanel != null
                || (_settingsOverlay != null && _settingsOverlay.IsOpen)
                || _isAnimating || _hammerArmed || _path.Count > 0 || _boosterCoachQueue.Count == 0) return;
            var next = _boosterCoachQueue[0];
            Transform target = null;
            foreach (var (b, node) in _boosterButtons) if (b == next) target = node;
            if (target == null) return;
            // Une apparition en cours fausserait le cadre visé : on attend qu'elle finisse.
            if (Tw.Has(target.gameObject, "appear"))
            {
                After(0.4f, ShowNextBoosterCoach, "coachRetry");
                return;
            }
            _boosterCoach = CoachMarkOverlay.Present(Root, target, Loc.T(next.CoachKey()), SceneSize);
            _s.Analytics.BoosterCoachShown(next.Id());
        }

        /// Ferme le coach-mark et marque le booster comme expliqué (à la fermeture, jamais à l'affichage).
        void DismissBoosterCoach(Vector2? point)
        {
            if (_boosterCoach == null) return;
            if (point is Vector2 p) _boosterCoach.HandleTouch(p); else _boosterCoach.Dismiss();
            if (_boosterCoachQueue.Count > 0)
            {
                _s.Economy.MarkBoosterCoachSeen(_boosterCoachQueue[0]);
                _boosterCoachQueue.RemoveAt(0);
            }
            _boosterCoach = null;
            After(0.25f, ShowNextBoosterCoach);
        }

        void AnimateBoosterAppearance(Transform node)
        {
            if (!JuiceSettings.MotionEnabled) return;
            node.localScale = Vector3.one * 0.1f;
            FxAlpha.Of(node.gameObject).Alpha = 0;
            Tw.Run(node.gameObject, Tw.Par(Tw.FadeIn(node.gameObject, 0.18f),
                Tw.Seq(Tw.Scale(node, 1.14f, 0.22f), Tw.Scale(node, 1f, 0.12f))), "appear");
            var halo = ShapeView.Circle("Halo", node, Vector2.zero, BoosterRadius, Color.clear, Look.Accent.WithAlpha(0.75f), 3, BarOrder - 3);
            Tw.Run(halo.gameObject, Tw.Seq(Tw.Par(Tw.Scale(halo.transform, 1.9f, 0.45f), Tw.FadeOut(halo.gameObject, 0.45f)), Tw.Remove(halo.gameObject)));
            HapticManager.Light();
        }

        void HandleBoosterTap(Booster booster)
        {
            if (_isAnimating) return;
            switch (booster)
            {
                case Booster.Hammer:
                    if (_s.Economy.BoosterCount(booster) > 0)
                    {
                        _hammerArmed = true;
                        _messageLabel.Text = Loc.T("booster.hammer_prompt");
                        _messageLabel.ShrinkToFit(640);
                        _messageLabel.gameObject.SetActive(true);
                        Tw.Kill(_messageLabel.gameObject, "flashMessage");
                    }
                    else DenyBooster(booster);
                    break;
                case Booster.Hint:
                    DisarmHammer();
                    // Le chemin d'abord : une grille sans solution ne coûte pas d'indice.
                    var path = _grid.ShowcasePath();
                    if (path == null) { FlashMessage(Loc.T("booster.hint_none")); return; }
                    if (AcquireBooster(booster)) ShowHint(path); else DenyBooster(booster);
                    break;
                case Booster.Shuffle:
                    DisarmHammer();
                    if (AcquireBooster(booster)) PerformShuffle(); else DenyBooster(booster);
                    break;
            }
        }

        void DisarmHammer()
        {
            if (!_hammerArmed) return;
            _hammerArmed = false;
            _messageLabel.gameObject.SetActive(false);
        }

        /// Consomme une unité de l'inventaire (jamais d'achat implicite).
        bool AcquireBooster(Booster booster)
        {
            if (!_s.Economy.ConsumeBooster(booster)) return false;
            _s.Analytics.BoosterUsed(booster.Id());
            RefreshBoosterButtons();
            return true;
        }

        void DenyBooster(Booster booster)
        {
            foreach (var (b, node) in _boosterButtons)
                if (b == booster)
                    Tw.Run(node.gameObject, Tw.Seq(Tw.MoveBy(node, P(-6, 0), 0.05f), Tw.MoveBy(node, P(12, 0), 0.05f), Tw.MoveBy(node, P(-6, 0), 0.05f)), "deny");
            PresentBoosterShopPrompt();
        }

        // MARK: Modale « à court de boosters »

        GridModel _pendingShopGrid;
        int _pendingShopScore;

        void PresentBoosterShopPrompt()
        {
            if (Kind != GameModeKind.Normal || _boosterShopPrompt != null || _gameOverPanel != null) return;
            // Instantané AVANT toute interaction.
            _hasPendingShopSnapshot = !_grid.IsGridEmpty();
            _pendingShopGrid = _grid.Copy();
            _pendingShopScore = _score;

            const int z = 150;
            var panel = RenderShared.Node("boosterPrompt", Root);
            FxAlpha.Of(panel.gameObject);
            var dim = ShapeView.Rect("Dim", panel, Vector2.zero, SceneSize * 2, 0, Colors.White(0, 0.45f), default, 0, z);
            AddZone("boosterPromptDim", dim.transform, SceneSize * 2);
            const float w = 470, h = 280;
            ShapeView.Rect("Card", panel, Vector2.zero, new Vector2(w, h), 32, new Color(0.98f, 0.96f, 0.93f, 1), Colors.White(0.70f, 0.25f), 1, z + 1);
            Label(panel, Loc.T("game.no_boosters_title"), Fonts.AvenirHeavy, 28, Colors.White(0.25f, 1), P(0, h / 2 - 58), z + 3).ShrinkToFit(w - 40);
            Label(panel, Loc.T("game.no_boosters_body"), Fonts.AvenirMedium, 17, Colors.White(0.38f, 1), P(0, 10), z + 3).Wrap(w - 72);
            AddPromptButton(panel, "boosterPromptShop", Loc.T("game.no_boosters_shop"), Look.Accent, Colors.White(0.20f, 1), new Vector2(-w / 4 - 6, -h / 2 + 58), z + 2);
            AddPromptButton(panel, "boosterPromptClose", Loc.T("game.no_boosters_continue"), Colors.White(0.90f, 1), Colors.White(0.35f, 1), new Vector2(w / 4 + 6, -h / 2 + 58), z + 2);
            _boosterShopPrompt = panel;
            FxAlpha.Of(panel.gameObject).Alpha = 0;
            Tw.Run(panel.gameObject, Tw.FadeIn(panel.gameObject, 0.18f));
        }

        void AddPromptButton(Transform panel, string name, string title, Color fill, Color ink, Vector2 pos, int z)
        {
            var node = RenderShared.Node(name, panel, P(pos.x, pos.y));
            Relief.Raise(ShapeView.Rect("Pill", node, Vector2.zero, new Vector2(190, 54), 27, fill, default, 0, z), 4);
            Label(node, title, Fonts.AvenirDemiBold, 19, ink, Vector2.zero, z + 3).ShrinkToFit(170);
            AddZone(name, node, new Vector2(190, 54));
        }

        void DismissBoosterShopPrompt()
        {
            Kill(_boosterShopPrompt);
            _boosterShopPrompt = null;
        }

        // MARK: Indice, mélange, marteau

        /// Indice : deux pulsations sur un chemin valide, sans rien consommer de la grille.
        void ShowHint(List<Cell> path)
        {
            IsAnimating = true;
            foreach (var c in path)
            {
                var n = _nodes[c.Row, c.Col];
                if (n == null) continue;
                Tw.Run(n.gameObject, Tw.Repeat(Tw.Seq(Tw.Scale(n.transform, 1.28f, 0.24f), Tw.Scale(n.transform, 1f, 0.24f)), 2), "hint");
            }
            After(1.05f, () => IsAnimating = false);
        }

        /// Mélange : réattribue les valeurs (toujours jouable) et reconstruit les bulles.
        void PerformShuffle()
        {
            CancelPath();
            IsAnimating = true;
            _grid.ReshuffleValues(SystemRng.Create());
            RebuildGridNodes();
            _lastFallen.Clear();
            _lastMoveHadFalls = false;
            HapticManager.Medium();
            After(0.25f, () => IsAnimating = false);
        }

        void RebuildGridNodes()
        {
            for (int r = 0; r < GameRules.Rows; r++)
            for (int c = 0; c < GameRules.Cols; c++)
            {
                if (_nodes[r, c] != null) Kill(_nodes[r, c].gameObject);
                _nodes[r, c] = null;
            }
            for (int r = 0; r < GameRules.Rows; r++)
            for (int c = 0; c < GameRules.Cols; c++)
            {
                var m = _grid.Cells[r][c];
                if (m == null) continue;
                var node = MakeBubble(m, ScenePos(r, c));
                node.transform.localScale = Vector3.one * 0.2f;
                Tw.Run(node.gameObject, Tw.Scale(node.transform, 1f, 0.2f), "appear");
                _nodes[r, c] = node;
            }
        }

        /// Marteau : détruit une bulle puis enchaîne sur la gravité.
        void PerformHammer(Cell coord)
        {
            if (_grid.Cells[coord.Row][coord.Col] == null) return;
            IsAnimating = true;
            var bubble = _nodes[coord.Row, coord.Col];
            if (bubble != null)
            {
                _juice.Burst(ScenePos(coord.Row, coord.Col), Look.BubbleColor(bubble.Value), PopTier.Small);
                bubble.PlayPop();
            }
            _nodes[coord.Row, coord.Col] = null;
            _grid.RemoveBubbles(new[] { coord });
            After(0.26f, AfterPop);
        }

        // MARK: - Vidéo récompensée : seconde chance

        /// Grille bloquée en mode normal : tire le mélange promis par « Continuer ».
        void PrepareSecondChance()
        {
            _secondChanceGrid = null;
            if (Kind != GameModeKind.Normal || _secondChanceUsed || !_s.Rewarded.CanOffer(RewardedPlacement.SecondChance)) return;
            var shuffled = _grid.Copy();
            shuffled.ReshuffleValues(SystemRng.Create());
            if (!shuffled.HasValidMove()) return;
            _secondChanceGrid = shuffled;
            DismissHintOffer();
            _s.Analytics.RewardedOfferView(RewardedPlacement.SecondChance.Id());
        }

        void WatchSecondChanceVideo()
        {
            _rewardedInFlight = true;
            _s.Rewarded.Show(RewardedPlacement.SecondChance, ApplySecondChance, rewarded =>
            {
                _rewardedInFlight = false;
                if (!rewarded) DropSecondChance();
            }, () =>
            {
                _rewardedInFlight = false;
                DropSecondChance();
            });
        }

        /// Vidéo regardée : la partie (déjà comptée) reprend sur le mélange promis, score gardé.
        void ApplySecondChance()
        {
            if (_disposed) return;
            if (_secondChanceGrid == null) { DropSecondChance(); return; }
            var shuffled = _secondChanceGrid;
            _secondChanceGrid = null;
            _secondChanceUsed = true;
            _continuedFrom = _lastResult;
            // La partie reprend : sa prochaine fin sera enregistrée (en continuation).
            _endRecorded = false;
            DismissNoAdsOffer();
            Kill(_gameOverOverlay);
            _gameOverOverlay = null;
            Kill(_gameOverPanel);
            _gameOverPanel = null;
            PruneZones();
            _messageLabel.gameObject.SetActive(false);
            _isWinState = false;
            CancelPath();
            _grid = shuffled;
            RebuildGridNodes();
            HapticManager.Medium();
            After(0.25f, () => IsAnimating = false);
        }

        /// Vidéo coupée ou indisponible : « Continuer » disparaît.
        void DropSecondChance()
        {
            _secondChanceGrid = null;
            var btn = FindNamed(_gameOverPanel, "continueBtn");
            if (btn == null) return;
            btn.name = "continueBtnGone";
            _zones.RemoveAll(z => z.Name == "continueBtn");
            Tw.Run(btn.gameObject, Tw.FadeOut(btn.gameObject, 0.2f));
        }

        // MARK: - Vidéo récompensée : indice gratuit

        /// Le joueur sèche depuis 20 s sans indice en poche : une pastille propose une vidéo.
        void MaybeOfferHintVideo()
        {
            if (_hintOfferNode != null || _rewardedInFlight || _hintVideosThisGame >= MaxHintVideosPerGame
                || FxTime.RealNow - _lastPlayerActionAt < HintOfferIdleDelay
                || _isAnimating || _hammerArmed || _path.Count > 0 || _gameOverPanel != null || _boosterShopPrompt != null
                || _boosterCoach != null || (_settingsOverlay != null && _settingsOverlay.IsOpen)
                || _s.Economy.BoosterCount(Booster.Hint) > 0 || !_s.Rewarded.CanOffer(RewardedPlacement.Hint)) return;
            if (_grid.ShowcasePath() == null) { _lastPlayerActionAt = FxTime.RealNow; return; }
            PresentHintOffer();
        }

        void PresentHintOffer()
        {
            var node = RenderShared.Node("hintOffer", Root, P(0, GridBottom - BubbleView.RadiusPt - 50));
            FxAlpha.Of(node.gameObject);
            ShapeView.Rect("Pill", node, Vector2.zero, new Vector2(260, 48), 24, Color.white, Look.Accent, 2, BarOrder);
            VectorIcon.Create(node, VectorIconName.BoosterHint, P(-100, 0), 24, IconInk, BarOrder + 2);
            Label(node, Loc.T("game.hint_video_offer"), Fonts.AvenirDemiBold, 18, Colors.White(0.30f, 1), Vector2.zero, BarOrder + 2).ShrinkToFit(160);
            VectorIcon.Create(node, VectorIconName.IconPlay, P(100, 0), 22, IconInk, BarOrder + 2);
            AddZone("hintOffer", node, new Vector2(260, 48));
            _hintOfferNode = node;
            if (JuiceSettings.MotionEnabled)
            {
                node.localScale = Vector3.one * 0.6f;
                FxAlpha.Of(node.gameObject).Alpha = 0;
                Tw.Run(node.gameObject, Tw.Par(Tw.FadeIn(node.gameObject, 0.2f), Tw.Scale(node, 1f, 0.24f)), "appear");
                Tw.Run(node.gameObject, Tw.Forever(Tw.Seq(Tw.Wait(2f), Tw.Scale(node, 1.06f, 0.15f), Tw.Scale(node, 1f, 0.15f))), "pulse");
            }
            _s.Analytics.RewardedOfferView(RewardedPlacement.Hint.Id());
        }

        void DismissHintOffer()
        {
            Kill(_hintOfferNode);
            _hintOfferNode = null;
        }

        void WatchHintVideo()
        {
            DismissHintOffer();
            _rewardedInFlight = true;
            _s.Rewarded.Show(RewardedPlacement.Hint, () =>
            {
                _hintVideosThisGame++;
                if (!_isAnimating && _grid.ShowcasePath() is List<Cell> path) ShowHint(path);
            }, _ =>
            {
                _rewardedInFlight = false;
                _lastPlayerActionAt = FxTime.RealNow;
            }, () =>
            {
                _rewardedInFlight = false;
                _lastPlayerActionAt = FxTime.RealNow;
            });
        }

        // MARK: - Graines de dix

        /// Nouvelle partie normale : la fleur éclose, s'il y en a une, est posée dans la grille.
        void PlantBloomedFlowerIfAny()
        {
            if (Kind != GameModeKind.Normal) return;
            if (_s.Seeds.TakeFlower() is not int power) return;
            if (_grid.PlantFlower(power, SystemRng.Create()) == null) return;
            _s.Analytics.FlowerPlaced(power);
            bool firstTime = !_s.Seeds.FlowerExplained;
            _s.Seeds.FlowerExplained = true;
            After(0.9f, () => FlashMessage(Loc.F(firstTime ? "game.flower_intro" : "game.flower_ready", power), firstTime ? 3.6f : 2.4f));
        }

        void ResetMirrorTracking()
        {
            _mirrorTally = new MirrorTally();
            _boardReadyAt = FxTime.RealNow;
            _pathStartedAt = null;
            _lastFallen.Clear();
            _lastMoveHadFalls = false;
        }
    }
}
