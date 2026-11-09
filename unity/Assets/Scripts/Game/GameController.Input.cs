using System.Collections.Generic;
using TenGo.Audio;
using TenGo.Core;
using TenGo.Fx;
using TenGo.Haptics;
using TenGo.Rendering;
using UnityEngine;

namespace TenGo.Game
{
    // MARK: - Toucher (touchesBegan / Moved / Ended / Cancelled)
    public sealed partial class GameController
    {
        /// Zone tactile nommée (équivalent de `nodes(at:)` + `node.name`).
        sealed class HitZone
        {
            public string Name;
            public Transform Node;
            public Vector2 SizePt;
            public bool Round;
        }

        readonly List<HitZone> _zones = new();

        void AddZone(string name, Transform node, Vector2 sizePt, bool round = false) =>
            _zones.Add(new HitZone { Name = name, Node = node, SizePt = sizePt, Round = round });

        /// Position d'un nœud en points de scène (la racine est à l'origine).
        static Vector2 WorldPt(Transform t) => (Vector2)t.position * Units.PointsPerUnit;

        /// Nom de la zone (la plus récente) sous `pt`, parmi celles dont le nom passe `filter`.
        string ZoneAt(Vector2 pt, System.Func<string, bool> filter = null)
        {
            for (int i = _zones.Count - 1; i >= 0; i--)
            {
                var z = _zones[i];
                if (z.Node == null || !z.Node.gameObject.activeInHierarchy) continue;
                if (filter != null && !filter(z.Name)) continue;
                var c = WorldPt(z.Node);
                var s = z.Node.lossyScale;
                var half = new Vector2(z.SizePt.x * Mathf.Abs(s.x), z.SizePt.y * Mathf.Abs(s.y)) / 2f;
                var d = pt - c;
                bool hit = z.Round ? d.magnitude <= half.x : Mathf.Abs(d.x) <= half.x && Mathf.Abs(d.y) <= half.y;
                if (hit) return z.Name;
            }
            return null;
        }

        void PruneZones() => _zones.RemoveAll(z => z.Node == null);

        static float Dist(Vector2 pt, Transform node) => (pt - WorldPt(node)).magnitude;

        public void PointerDown(Vector2 point)
        {
            if (Kind == GameModeKind.Demo) return;
            PruneZones();

            // Réglages ouverts : prioritaires.
            if (_settingsOverlay != null && _settingsOverlay.IsOpen)
            {
                _settingsOverlay.HandleTouch(point);
                if (!_settingsOverlay.IsOpen) _settingsOverlay = null;
                return;
            }

            if (_rewardedInFlight) return;
            _lastPlayerActionAt = FxTime.RealNow;

            // Pastille « indice gratuit » : un tap dessus lance la vidéo, tout autre tap la retire.
            if (_hintOfferNode != null)
            {
                if (ZoneAt(point, n => n == "hintOffer") != null) { WatchHintVideo(); return; }
                DismissHintOffer();
            }

            // Modale « à court de boosters » : bloque le reste du geste.
            if (_boosterShopPrompt != null)
            {
                switch (ZoneAt(point, n => n.StartsWith("boosterPrompt")))
                {
                    case "boosterPromptShop": NavigateToBoutique("empty_bar"); return;
                    case "boosterPromptClose":
                    case "boosterPromptDim":
                        DismissBoosterShopPrompt();
                        _hasPendingShopSnapshot = false;
                        return;
                }
                return;
            }

            // Coach-mark : n'importe quel tap le referme et passe au suivant (tap consommé).
            if (_boosterCoach != null) { DismissBoosterCoach(point); return; }

            // Offre « sans pub » sous le panneau de fin.
            if (HandleNoAdsOfferTouch(point)) return;

            if (_settingsBubbleNode != null && Dist(point, _settingsBubbleNode) < 34)
            {
                Press(_settingsBubbleNode);
                PresentSettings();
                return;
            }

            if (_homeBubbleNode.gameObject.activeSelf && Dist(point, _homeBubbleNode) < 34)
            {
                HapticManager.Light();
                GoBackToMenu();
                return;
            }

            if (_gameOverPanel != null)
            {
                HandlePanelTouch(point);
                return;   // le panneau bloque le reste du geste
            }

            // « Recommencer » : mode normal seulement.
            if (Kind == GameModeKind.Normal && Dist(point, _restartBubbleNode) < 55)
            {
                Press(_restartBubbleNode);
                ResetGame();
                return;
            }

            if (_boosterBar != null && _boosterBar.gameObject.activeInHierarchy && !_isAnimating)
            {
                if (_shopPillNode != null)
                {
                    var p = WorldPt(_shopPillNode);
                    if (Mathf.Abs(point.x - p.x) < 98 && Mathf.Abs(point.y - p.y) < 26)
                    {
                        Press(_shopPillNode, 0.93f, 0.07f, 0.1f);
                        _hasPendingShopSnapshot = !_grid.IsGridEmpty();
                        After(0.12f, () => NavigateToBoutique("empty_bar"));
                        return;
                    }
                }
                foreach (var (booster, node) in _boosterButtons)
                {
                    if (Dist(point, node) < _boosterHitRadius)
                    {
                        Press(node);
                        HandleBoosterTap(booster);
                        return;
                    }
                }
            }

            // Marteau armé : le prochain tap sur une bulle la détruit.
            if (_hammerArmed)
            {
                _hammerArmed = false;
                _messageLabel.gameObject.SetActive(false);
                if (!_isAnimating && GridCoord(point) is Cell coord && _grid.Cells[coord.Row][coord.Col]?.IsFrozen != true)
                {
                    if (_s.Economy.ConsumeBooster(Booster.Hammer))
                    {
                        RefreshBoosterButtons();
                        PerformHammer(coord);
                    }
                }
                return;
            }

            if (_rushCountdownActive) return;

            // Une animation tourne : le geste est retenu, pas jeté.
            if (_isAnimating) { _pendingTouchPoint = point; return; }
            BeginPath(point);
        }

        void BeginPath(Vector2 point)
        {
            if (GridCoord(point) is not Cell coord) return;
            if (_grid.Cells[coord.Row][coord.Col]?.IsFrozen == true) return;
            _path.Clear();
            _path.Add(coord);
            _pathStartedAt = FxTime.RealNow;
            int value = _grid.Cells[coord.Row][coord.Col]?.Value ?? 1;
            _nodes[coord.Row, coord.Col]?.SetSelected(true, PathTension);
            SoundManager.Shared.PlaySelect(value);
            _juice.OnSelect();
            UpdatePathLine();
        }

        /// L'animation est finie et un doigt est resté posé : le chemin démarre
        /// là où ce doigt est MAINTENANT.
        void ResumePendingTouchIfNeeded()
        {
            if (_pendingTouchPoint is not Vector2 point) return;
            _pendingTouchPoint = null;
            if (Kind == GameModeKind.Demo || _rushCountdownActive || _gameOverPanel != null || _path.Count > 0) return;
            BeginPath(point);
        }

        public void PointerMove(Vector2 point)
        {
            if (_isAnimating)
            {
                if (_pendingTouchPoint != null) _pendingTouchPoint = point;
                return;
            }
            if (_rushCountdownActive || _path.Count == 0) return;
            if (DragCoord(point) is not Cell target) return;
            var last = _path[_path.Count - 1];
            if (target == last) return;

            // Retour en arrière étendu : la cible est déjà dans le chemin → on tronque jusqu'à elle.
            int idx = _path.IndexOf(target);
            if (idx >= 0 && idx < _path.Count - 1)
            {
                while (_path.Count > idx + 1)
                {
                    var removed = _path[_path.Count - 1];
                    _path.RemoveAt(_path.Count - 1);
                    _nodes[removed.Row, removed.Col]?.SetSelected(false);
                }
                SoundManager.Shared.PlayBacktrack();
                UpdatePathLine();
                return;
            }
            WalkPath(target);
        }

        /// Avance d'un pas « roi » à la fois vers `target` (rattrape un glissé diagonal rapide).
        void WalkPath(Cell target)
        {
            while (_path.Count > 0)
            {
                var last = _path[_path.Count - 1];
                if (last == target) break;
                var next = new Cell(last.Row + System.Math.Sign(target.Row - last.Row), last.Col + System.Math.Sign(target.Col - last.Col));
                if (!TryAppendCell(next)) break;
            }
        }

        bool TryAppendCell(Cell coord)
        {
            if (coord.Row < 0 || coord.Row >= GameRules.Rows || coord.Col < 0 || coord.Col >= GameRules.Cols) return false;
            if (_path.Contains(coord)) return false;
            var bubble = _grid.Cells[coord.Row][coord.Col];
            if (bubble == null || bubble.IsFrozen) return false;
            // Somme dépassée : refus signalé (« pas celle-là »), sans révéler de chiffre.
            if (_grid.PathSum(_path) + bubble.Value > GameRules.TargetSum)
            {
                var node = _nodes[coord.Row, coord.Col];
                if (node != null) _juice.OnRejected(node, coord.Row, coord.Col);
                return false;
            }
            _path.Add(coord);
            float tension = PathTension;
            _nodes[coord.Row, coord.Col]?.SetSelected(true, tension);
            SoundManager.Shared.PlayConnect(bubble.Value, tension);
            _juice.OnAppend(tension);
            UpdatePathLine();
            return true;
        }

        public void PointerUp(Vector2 point)
        {
            // Avant toute sortie : un doigt relâché pendant l'animation ne laisse pas de geste en attente.
            _pendingTouchPoint = null;
            if (_isAnimating || _rushCountdownActive || _path.Count == 0) return;
            if (_grid.PathSum(_path) == GameRules.TargetSum) CommitPath();
            else CancelPath();
        }

        public void PointerCancel()
        {
            _pendingTouchPoint = null;
            CancelPath();
        }
    }
}
