using System.Collections.Generic;
using TenGo.Audio;
using TenGo.Core;
using TenGo.Fx;
using TenGo.Rendering;
using UnityEngine;

namespace TenGo.Game
{
    // MARK: - Tracé, score, validation, gravité
    public sealed partial class GameController
    {
        static readonly Color GainOrange = new(0.92f, 0.55f, 0.14f, 1);

        void UpdatePathLine()
        {
            float tension = PathTension;
            // Toutes les bulles du chemin suivent la tension.
            foreach (var c in _path) _nodes[c.Row, c.Col]?.SetSelected(true, tension);

            // Le trait est reconstruit ; la comète vit sur le conteneur et survit.
            Kill(_strokeNode);
            _strokeNode = null;

            if (_path.Count < 2)
            {
                Kill(_trailContainer);
                _trailContainer = null;
                _trailComet = null;
                UpdatePathGainLabel();
                return;
            }
            if (_trailContainer == null) _trailContainer = RenderShared.Node("TrailContainer", Root);

            var pts = new List<Vector2>(_path.Count);
            foreach (var c in _path) pts.Add(ScenePos(c.Row, c.Col));
            _strokeNode = Rendering.TrailRenderer.Make(_trailContainer, pts, Look.Trail, Look.Accent, tension);
            var head = _path[_path.Count - 1];
            _trailComet = Rendering.TrailRenderer.MoveComet(_trailComet, _trailContainer, ScenePos(head.Row, head.Col), tension, Look.Accent);
            UpdatePathGainLabel();
        }

        /// « +N » au-dessus du doigt, seulement à somme exactement 10 : le barème
        /// devient lisible. Refonte 4.0 : chiffre encre dans une pastille blanche à filet.
        void UpdatePathGainLabel()
        {
            if (_path.Count < 2 || _grid.PathSum(_path) != GameRules.TargetSum)
            {
                KillPathGain();
                return;
            }
            int points = Scoring.ForPath(_path.Count) * _grid.BloomMultiplier(_path);
            if (_pathGainLabel == null)
            {
                var node = RenderShared.Node("PathGain", Root);
                _pathGainLabel = Label(node, "", NunitoWeight.Black, 30, Ui.Design.Ink, Vector2.zero, Layers.PathGain + 2);
            }
            var label = _pathGainLabel;
            var holder = label.transform.parent;
            var tier = PopTiers.ForLength(_path.Count);
            string previous = label.Text;
            label.Text = "+" + points;
            float sizePt = tier == PopTier.Small ? 20 : tier == PopTier.Medium ? 24 : 28;
            label.SizePt = Ui.Design.Pt(sizePt);
            label.Color = points >= 200 ? GainOrange : Ui.Design.Ink;
            if (previous != label.Text)
            {
                var oldBg = holder.Find("Pill");
                if (oldBg != null) Kill(oldBg);
                var oldHair = holder.Find("PillHair");
                if (oldHair != null) Kill(oldHair);
                float h = Ui.Design.Pt(sizePt * 1.36f + 4), w = label.WidthPt() + Ui.Design.Pt(20);
                Ui.Design.Surface("Pill", holder, Vector2.zero, new Vector2(w, h), Ui.Design.Pt(12), Layers.PathGain);
            }
            var head = _path[_path.Count - 1];
            var target = ScenePt(head.Row, head.Col);
            holder.localPosition = P(target.x, target.y + _bubbleR * (36.5f / 23.5f) + Ui.Design.Pt(sizePt - 20) * 0.5f);
            if (previous != label.Text)
            {
                holder.localScale = Vector3.one;
                Tw.Run(holder.gameObject, Tw.Seq(Tw.Scale(holder, 1.18f, 0.07f), Tw.Scale(holder, 1f, 0.11f)), "bump");
            }
        }

        void KillPathGain()
        {
            if (_pathGainLabel != null) Kill(_pathGainLabel.transform.parent);
            _pathGainLabel = null;
        }

        void CancelPath()
        {
            foreach (var c in _path) _nodes[c.Row, c.Col]?.SetSelected(false);
            _path.Clear();
            ClearTrail();
            SoundManager.Shared.CancelPath();
        }

        void ClearTrail()
        {
            KillPathGain();
            Kill(_strokeNode);
            _strokeNode = null;
            if (_trailComet != null) Kill(_trailComet.gameObject);
            _trailComet = null;
            Kill(_trailContainer);
            _trailContainer = null;
        }

        // MARK: - Score

        void UpdateScoreLabel() => SetScoreText(_displayedScore);

        /// Aligne l'affichage sur le score réel (fin de partie : jamais un compteur en retard).
        void SyncDisplayedScore()
        {
            if (_displayedScore == _score) return;
            _displayedScore = _score;
            UpdateScoreLabel();
        }

        /// « +N » dans une mini-bulle qui éclot sur la dernière bulle puis vole
        /// (courbe de Bézier quadratique) vers le score, qui ne monte qu'à l'arrivée.
        void ShowScorePopup(int points, Vector2 bubblePt, PopTier tier = PopTier.Small)
        {
            bool big = points >= 100;
            float radius = big ? 32 : 26;
            var tint = big ? new Color(0.92f, 0.62f, 0.18f, 1) : Colors.White(0.3f, 0.85f);

            var flight = RenderShared.Node("ScoreFlight", Root, P(bubblePt.x, bubblePt.y + 30));
            FxAlpha.Of(flight.gameObject);
            flight.localScale = Vector3.zero;

            if (tier == PopTier.Large && JuiceSettings.MotionEnabled)
            {
                var halo = ShapeView.Dot("Halo", flight, Vector2.zero, radius * 4, new Color(0.98f, 0.80f, 0.35f, 0.55f), true, 199);
                Tw.Run(halo.gameObject, Tw.Seq(Tw.Par(Tw.Scale(halo.transform, 1.5f, 0.35f), Tw.FadeOut(halo.gameObject, 0.35f)), Tw.Remove(halo.gameObject)));
            }
            ShapeView.Circle("Circle", flight, Vector2.zero, radius, Ui.Design.Card, big ? tint.WithAlpha(0.8f) : Ui.Design.Hair, 2, 200);
            Label(flight, "+" + points, big ? Fonts.AvenirHeavy : Fonts.AvenirDemiBold, big ? 26 : 20, tint, Vector2.zero, 202);

            Tw.Run(flight.gameObject, Tw.Seq(
                Tw.Scale(flight, 1.15f, 0.10f),
                Tw.Scale(flight, 1f, 0.05f),
                Tw.Wait(0.25f),
                Tw.Call(() =>
                {
                    if (flight == null || _disposed) return;
                    // La cible est lue MAINTENANT : la mise en page a pu bouger.
                    Vector2 from = flight.localPosition;
                    Vector2 to = _scoreBubbleNode.localPosition;
                    var mid = (from + to) / 2f;
                    var ctrl = new Vector2(mid.x + Units.FromPoints(from.x <= to.x ? 80 : -80), mid.y);
                    Tw.Run(flight.gameObject, Tw.Seq(
                        Tw.Par(
                            Tw.Custom(0.5f, t =>
                            {
                                float e = t * t;   // easeIn
                                float u = 1 - e;
                                flight.localPosition = u * u * from + 2 * u * e * ctrl + e * e * to;
                            }),
                            Tw.Scale(flight, 0.45f, 0.5f),
                            Tw.Fade(flight.gameObject, 0.7f, 0.5f)),
                        Tw.Call(() =>
                        {
                            if (_disposed) return;
                            _displayedScore = Mathf.Min(_displayedScore + points, _score);
                            UpdateScoreLabel();
                            Tw.Run(_scoreBubbleNode.gameObject, Tw.Seq(Tw.Scale(_scoreBubbleNode, _scoreBubbleNode.localScale.x * 1.18f, 0.07f),
                                Tw.Scale(_scoreBubbleNode, 1f, 0.12f)), "pulse");
                        }),
                        Tw.Remove(flight.gameObject)), "fly");
                })), "pop");
        }

        // MARK: - Validation

        void CommitPath()
        {
            IsAnimating = true;
            ClearTrail();

            int bloom = _grid.BloomMultiplier(_path);
            int points = Scoring.ForPath(_path.Count) * bloom;
            var last = _path[_path.Count - 1];
            var popupOrigin = ScenePt(last.Row, last.Col);
            var pathCopy = new List<Cell>(_path);
            int length = pathCopy.Count;
            _combosCreated++;
            _longestChain = Mathf.Max(_longestChain, length);
            _path.Clear();

            if (Kind != GameModeKind.Demo)
            {
                var values = new List<int>();
                bool usedFallen = false;
                foreach (var c in pathCopy)
                {
                    if (_grid.Cells[c.Row][c.Col] is BubbleModel b) values.Add(b.Value);
                    if (_lastFallen.Contains(c.Index)) usedFallen = true;
                }
                double? look = _pathStartedAt.HasValue ? _pathStartedAt.Value - _boardReadyAt : (double?)null;
                _mirrorTally.Record(values, look, usedFallen, _lastMoveHadFalls);
                if (bloom > 1)
                {
                    _s.Analytics.FlowerUsed(bloom, length);
                    FlashMessage(Loc.F("game.flower_picked", bloom), 1.8f);
                }
            }
            _pathStartedAt = null;
            if (Kind == GameModeKind.Rush && length >= RushBonusChainLength) AwardRushTimeBonus();

            // Battre sa plus longue chaîne de la partie fait monter le ressenti d'un cran.
            var tier = PopTiers.ForLength(length);
            if (length > _celebratedChain && length >= 3) { _celebratedChain = length; tier = tier.Boosted(); }
            if (bloom > 1) tier = tier.Boosted();

            // Score réel crédité tout de suite ; l'affichage attend la bulle « +N ».
            _score += points;
            if (Kind != GameModeKind.Demo) AnnounceAchievements(_s.Coordinator.OnChainCommitted(Mode, length, points));
            ShowScorePopup(points, popupOrigin, tier);

            SoundManager.Shared.PlayCombo(length);
            _juice.OnCommit(tier, length);
            PushNeighbors(pathCopy, tier);

            for (int i = 0; i < pathCopy.Count; i++)
            {
                var c = pathCopy[i];
                var bubble = _nodes[c.Row, c.Col];
                if (bubble != null)
                {
                    var burst = _juice.Burst(ScenePos(c.Row, c.Col), Look.BubbleColor(bubble.Value), tier);
                    if (burst != null && i > 0)
                    {
                        var a = FxAlpha.Of(burst.gameObject);
                        a.Alpha = 0;
                        Tw.Run(burst.gameObject, Tw.Seq(Tw.Wait(i * 0.020f), Tw.FadeIn(burst.gameObject, 0.01f)));
                    }
                    // Décalage plafonné : tout doit avoir disparu avant la gravité (0,26 s).
                    float popDelay = Mathf.Min(i * 0.012f, 0.08f);
                    var b = bubble;
                    Tw.Run(b.gameObject, Tw.Seq(Tw.Wait(popDelay), Tw.Call(() => { if (b != null) b.PlayPop(); })), "popDelay");
                }
                _nodes[c.Row, c.Col] = null;
            }
            _grid.RemoveBubbles(pathCopy);

            // Défi du jour : un chemin adjacent fait fondre le givre.
            foreach (var pos in _grid.ThawFrozenBubbles(pathCopy)) _nodes[pos.Row, pos.Col]?.Thaw();

            After(0.26f, AfterPop);
        }

        void AnnounceAchievements(IReadOnlyList<AchievementDefinition> unlocked)
        {
            if (unlocked == null || unlocked.Count == 0) return;
            string text = unlocked.Count > 1 ? Loc.F("achievements.unlocked_multi", unlocked.Count) : Loc.T(unlocked[0].TitleKey);
            FlashMessage(text, 2.2f);
        }

        /// Souffle de la chaîne sur les voisines (décoratif, le modèle ne bouge pas).
        void PushNeighbors(List<Cell> path, PopTier tier)
        {
            float push = tier.NeighborPush();
            if (push <= 0 || !JuiceSettings.MotionEnabled) return;
            var inPath = new HashSet<int>();
            foreach (var c in path) inPath.Add(c.Index);
            var pushed = new HashSet<int>();
            foreach (var c in path)
            for (int dr = -1; dr <= 1; dr++)
            for (int dc = -1; dc <= 1; dc++)
            {
                if (dr == 0 && dc == 0) continue;
                int r = c.Row + dr, cc = c.Col + dc;
                if (r < 0 || r >= GameRules.Rows || cc < 0 || cc >= GameRules.Cols) continue;
                int key = r * GameRules.Cols + cc;
                if (inPath.Contains(key) || pushed.Contains(key)) continue;
                var node = _nodes[r, cc];
                if (node == null) continue;
                pushed.Add(key);
                node.Nudge(dc * push, dr * push);
            }
        }

        void AfterPop()
        {
            if (_grid.IsGridEmpty())
            {
                if (Kind == GameModeKind.Demo) { if (_demoScript == null) DemoReseed(); else TriggerWin(); }
                else if (Kind == GameModeKind.Rush) RushBoardCleared();
                else TriggerWin();
                return;
            }

            var falls = _grid.ApplyGravity();
            _lastFallen.Clear();
            foreach (var f in falls) _lastFallen.Add(f.ToRow * GameRules.Cols + f.Col);
            _lastMoveHadFalls = falls.Count > 0;

            var moves = new List<(BubbleView node, int from, int to, int col)>();
            foreach (var f in falls)
                if (_nodes[f.FromRow, f.Col] is BubbleView n) moves.Add((n, f.FromRow, f.ToRow, f.Col));
            foreach (var m in moves) _nodes[m.from, m.col] = null;
            foreach (var m in moves) _nodes[m.to, m.col] = m.node;

            float maxDuration = 0;
            foreach (var m in moves)
            {
                float d = Mathf.Min(0.08f + 0.04f * (m.from - m.to), 0.4f);
                maxDuration = Mathf.Max(maxDuration, d);
                m.node.PlayFall(Units.FromPoints(GridOriginY + m.to * CellSize), d);
            }
            if (moves.Count == 0) AfterGravity();
            else After(maxDuration + 0.05f, AfterGravity);
        }

        void AfterGravity()
        {
            if (!_grid.HasValidMove())
            {
                if (Kind == GameModeKind.Demo) DemoReseed();
                else if (Kind == GameModeKind.Rush)
                {
                    // Le chrono seul décide de la fin : on remélange, sinon on redistribue.
                    PerformShuffle();
                    if (!_grid.HasValidMove()) RushReseed();
                }
                else
                {
                    PrepareSecondChance();
                    TriggerLose();
                }
            }
            else
            {
                IsAnimating = false;
                ShowNextBoosterCoach();
            }
        }

        void FlashMessage(string text, float duration = 1.6f)
        {
            _messageLabel.Text = text;
            _messageLabel.ShrinkToFit(640);
            _messageLabel.gameObject.SetActive(true);
            Tw.Run(_messageLabel.gameObject, Tw.Seq(Tw.Wait(duration), Tw.Call(() =>
            {
                if (!_hammerArmed) _messageLabel.gameObject.SetActive(false);
            })), "flashMessage");
        }
    }
}
