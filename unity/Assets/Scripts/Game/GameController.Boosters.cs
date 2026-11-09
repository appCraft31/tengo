using System.Collections.Generic;
using TenGo.Core;
using TenGo.Fx;
using TenGo.Haptics;
using TenGo.Platform;
using TenGo.Rendering;
using TenGo.Ui;
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
            Booster.Hint => VectorIconName.LineBulb,
            Booster.Shuffle => VectorIconName.LineShuffle,
            _ => VectorIconName.LineHammer,
        };

        /// Libellé sous le bouton (Indice, Mélange, Marteau).
        static string TitleOf(Booster b) => Loc.T("booster." + b.Id());

        void SetupBoosters()
        {
            if (Kind != GameModeKind.Normal) return;
            _boosterBar = RenderShared.Node("boosterBar", Root);
            RefreshBoosterButtons();
        }

        /// Booster de la barre (refonte 4.0) : carré blanc à filet, icône au trait,
        /// libellé dessous et compteur en pastille encre ; stock vide = « + » vert
        /// qui mène à la boutique (modale « à court de boosters »).
        Transform MakeBoosterButton(Booster booster)
        {
            var node = MakeBarButton("booster_" + booster.Id(), IconOf(booster), TitleOf(booster), small: false, _boosterBar);
            var badge = RenderShared.Node("Badge", node, P(Design.Pt(26), Design.Pt(26)));
            _boosterCountLabels[booster] = Design.Text(badge, "", NunitoWeight.Black, 12, Color.white, Vector2.zero, BarOrder + 6);
            return node;
        }

        /// Pastille du compteur : encre avec le nombre, ou accent avec « + » quand le stock est vide.
        void SyncBoosterBadge(Booster booster, Transform node)
        {
            if (!_boosterCountLabels.TryGetValue(booster, out var label)) return;
            var badge = label.transform.parent;
            var old = badge.Find("Bg");
            if (old != null) Kill(old);
            int count = _s.Economy.BoosterCount(booster);
            bool empty = count <= 0;
            label.Text = empty ? "+" : count.ToString();
            label.Color = empty ? Design.Ink : Color.white;
            float h = Design.Pt(24);
            float w = Mathf.Max(h, label.WidthPt() + Design.Pt(12));
            var bg = ShapeView.Rect("Bg", badge, Vector2.zero, new Vector2(w, h), h / 2, empty ? Design.Accent : Design.Ink, default, 0, BarOrder + 5);
            bg.transform.SetAsFirstSibling();
        }

        /// Les trois boosters sont toujours affichés, avec leur stock.
        void RefreshBoosterButtons()
        {
            if (Kind != GameModeKind.Normal || _boosterBar == null) return;
            bool built = _boosterButtons.Count > 0;
            if (!built)
            {
                foreach (var b in BoosterOrder) _boosterButtons.Add((b, MakeBoosterButton(b)));
                RepositionBottomRow();
            }
            foreach (var (b, node) in _boosterButtons) SyncBoosterBadge(b, node);
            QueueBoosterCoachMarks();
        }

        // MARK: Coach-marks

        void QueueBoosterCoachMarks()
        {
            var pending = new List<Booster>();
            foreach (var (b, _) in _boosterButtons)
                if (_s.Economy.BoosterCount(b) > 0 && !_s.Economy.BoosterCoachSeen(b)) pending.Add(b);
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
            var node = RenderShared.Node("hintOffer", Root, P(0, GridEdgeBottom - Design.Pt(52)));
            FxAlpha.Of(node.gameObject);
            ShapeView.Rect("Pill", node, Vector2.zero, new Vector2(260, 48), 24, Design.Card, Design.Accent, 2, BarOrder);
            VectorIcon.Create(node, VectorIconName.LineBulb, P(-100, 0), 26, Design.Ink, BarOrder + 2);
            Label(node, Loc.T("game.hint_video_offer"), NunitoWeight.ExtraBold, 18, Design.Ink, Vector2.zero, BarOrder + 2).ShrinkToFit(160);
            VectorIcon.Create(node, VectorIconName.LineVideo, P(100, 0), 24, Design.Ink, BarOrder + 2);
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
            // Fleur cueillie : l'annonce d'éclosion n'a plus lieu d'être (`cancelSeedBloom`).
            _s.Play.CancelSeedBloom();
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
