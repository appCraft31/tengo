using TenGo.Core;
using TenGo.Fx;
using TenGo.Rendering;
using UnityEngine;

namespace TenGo.Game
{
    // MARK: - Mise en page (setupUI, setupGrid, repositionBottomRow, layoutTopRow, layoutBottomBand)
    public sealed partial class GameController
    {
        static readonly Color ControlInk = Colors.White(0.45f, 1);

        Transform _homeShapeRoot;
        Transform _scoreShapeRoot;
        bool _topRowLayout;

        void SetupUI()
        {
            float logoY = GridTop + 95;
            var logo = Look.Logo;

            if (Kind == GameModeKind.Puzzle && _puzzleLevel != null)
            {
                Label(Root, Loc.F("puzzle.level_label", _puzzleLevel.Index), Fonts.AvenirHeavy, 34, logo, P(0, logoY + 8), 10);
                Label(Root, Loc.T("puzzle.goal_clear"), Fonts.AvenirMedium, 15, logo.WithAlpha(0.65f), P(0, logoY - 22), 10);
            }
            else if (Kind == GameModeKind.Rush)
            {
                _rushTimerLabel = Label(Root, "60", Fonts.AvenirHeavy, 48, logo, P(0, logoY), 10);
            }
            else
            {
                Label(Root, "TEN", Fonts.AvenirHeavy, 48, logo, P(-14, logoY), 10, TMPro.HorizontalAlignmentOptions.Right)
                    .Tmp.rectTransform.pivot = new Vector2(1, 0.5f);
                var palette = Look.Theme.Bubbles;
                var dot = ShapeView.Circle("LogoDot", Root, P(0, logoY + 3), 11,
                    Colors.FromRgb(palette[UnityEngine.Random.Range(0, palette.Count)]), default, 0, 11);
                Tw.Run(dot.gameObject, Tw.Forever(Tw.Seq(Tw.Scale(dot.transform, 1.15f, 1.4f), Tw.Scale(dot.transform, 0.90f, 1.4f))), "pulse");
                Label(Root, "GO", Fonts.AvenirHeavy, 48, logo, P(14, logoY), 10, TMPro.HorizontalAlignmentOptions.Left)
                    .Tmp.rectTransform.pivot = new Vector2(0, 0.5f);
            }

            _messageLabel = Label(Root, "", Fonts.AvenirHeavy, 28, Colors.White(0.35f, 1), P(0, GridTop + 50), 60);
            _messageLabel.gameObject.SetActive(false);

            // Deux bulles côte à côte, centrées dans l'espace sous la grille.
            float sceneBottom = -SceneSize.y / 2;
            float gridEdgeBottom = GridBottom - BubbleView.RadiusPt;
            float rowY = gridEdgeBottom - (gridEdgeBottom - sceneBottom) / 2;
            const float scoreR = 58, sideR = 44, gap = 18;
            const float sideCenterX = scoreR + gap + sideR;

            _homeBubbleNode = RenderShared.Node("homeBubble", Root, P(-sideCenterX, rowY));
            _homeShapeRoot = RenderShared.Node("shape", _homeBubbleNode);
            Relief.Raise(ShapeView.Circle("Circle", _homeShapeRoot, Vector2.zero, sideR, Color.white, default, 0, 20), 4, fill: Color.white);
            _homeGlyph = Glyphs.Home(_homeBubbleNode, Vector2.zero, 28, ControlInk, 23);

            _scoreBubbleNode = RenderShared.Node("scoreBubble", Root, P(0, rowY));
            _scoreShapeRoot = RenderShared.Node("scoreShape", _scoreBubbleNode);
            Relief.Raise(ShapeView.Circle("Circle", _scoreShapeRoot, Vector2.zero, scoreR, Color.white, default, 0, 20), 5, fill: Color.white);
            _scoreCaption = Label(_scoreBubbleNode, Loc.T("game.points_label"), Fonts.AvenirMedium, 13, Colors.White(0.58f, 1), P(0, 22), 23);
            _scoreLabel = Label(_scoreBubbleNode, "0", Fonts.AvenirMedium, 32, Colors.White(0.32f, 1), P(0, -8), 23);

            _restartBubbleNode = RenderShared.Node("restartBubble", Root, P(sideCenterX, rowY));
            Relief.Raise(ShapeView.Circle("Circle", _restartBubbleNode, Vector2.zero, sideR, Color.white, default, 0, 20), 4, fill: Color.white);
            Glyphs.Restart(_restartBubbleNode, Vector2.zero, 30, ControlInk, 23);
            // Défi du jour : une seule partie par jour, pas de rejeu.
            _restartBubbleNode.gameObject.SetActive(Kind != GameModeKind.Daily);
        }

        void SetupGrid()
        {
            RenderObstacles();
            for (int r = 0; r < GameRules.Rows; r++)
            for (int c = 0; c < GameRules.Cols; c++)
            {
                var m = _grid.Cells[r][c];
                if (m == null) continue;
                _nodes[r, c] = MakeBubble(m, ScenePos(r, c));
            }
        }

        BubbleView MakeBubble(BubbleModel m, Vector2 pos)
        {
            var node = BubbleView.Create(Root, m.Value, pos, Layers.Bubbles);
            if (m.IsAnchored) node.SetAnchored(true);
            if (m.IsFrozen) node.SetFrozen(true);
            if (m.Bloom > 1) node.SetBloom(m.Bloom);
            return node;
        }

        /// Pierres-obstacles du défi du jour (cases bloquées, statiques).
        void RenderObstacles()
        {
            foreach (var o in _obstacleNodes) Kill(o);
            _obstacleNodes.Clear();
            for (int r = 0; r < GameRules.Rows; r++)
            for (int c = 0; c < GameRules.Cols; c++)
                if (_grid.Blocked[r][c])
                    _obstacleNodes.Add(BubbleView.MakeObstacle(Root, ScenePos(r, c), CellSize, Layers.Bubbles).transform);
        }

        // MARK: - Coordonnées

        static Vector2 ScenePos(int row, int col) => P(GridOriginX + col * CellSize, GridOriginY + row * CellSize);

        /// `scenePos` en points de scène.
        static Vector2 ScenePt(int row, int col) => new(GridOriginX + col * CellSize, GridOriginY + row * CellSize);

        Cell? GridCoord(Vector2 pt)
        {
            float fx = (pt.x - GridOriginX + CellSize / 2) / CellSize;
            float fy = (pt.y - GridOriginY + CellSize / 2) / CellSize;
            // `Int(_:)` de Swift tronque vers zéro : -0,3 donne 0, comme ici.
            int col = (int)fx, row = (int)fy;
            if (row < 0 || row >= GameRules.Rows || col < 0 || col >= GameRules.Cols) return null;
            if (_grid.Cells[row][col] == null) return null;
            return new Cell(row, col);
        }

        /// Pendant le glissé, la case n'est prise que près de son centre (0,42 × la case) :
        /// les coins où se croisent quatre cases restent neutres.
        Cell? DragCoord(Vector2 pt)
        {
            if (GridCoord(pt) is not Cell c) return null;
            var center = ScenePt(c.Row, c.Col);
            return (pt - center).magnitude <= CellSize * 0.42f ? c : null;
        }

        // MARK: - Disposition (rangée haute ou historique)

        void RepositionBottomRow(bool animated = false)
        {
            var g = _geometry;
            float visibleBottom = g.VisibleBottom;
            float gridEdgeBottom = GridBottom - BubbleView.RadiusPt;
            float band = gridEdgeBottom - visibleBottom;
            float bandMid = (gridEdgeBottom + visibleBottom) / 2;

            // Écran assez haut : accueil, score et réglages en rangée haute ; la
            // bande du bas ne porte que les boosters et « recommencer ».
            float topRowY = -visibleBottom - g.TopInset - 26;
            if (Kind != GameModeKind.Demo && topRowY - 70 > GridTop + 95 + 30)
            {
                LayoutTopRow(topRowY, g.VisibleWidth / 2 - 58);
                LayoutBottomBand(bandMid, animated);
                return;
            }

            const float sideX = 120;
            if (_boosterBar == null || (_boosterButtons.Count == 0 && _shopPillNode == null))
            {
                _homeBubbleNode.localPosition = P(-sideX, bandMid);
                _scoreBubbleNode.localPosition = P(0, bandMid);
                _restartBubbleNode.localPosition = P(sideX, bandMid);
                return;
            }
            int n = _boosterButtons.Count;
            if (_shopPillNode != null) _shopPillNode.localPosition = Vector3.zero;

            float twoRowNeeded = BoosterRadius * 2 + 28 + 116;
            if (band >= twoRowNeeded)
            {
                const float rowGap = 116, spacing = 132;
                _boosterBar.localPosition = P(0, bandMid + rowGap / 2);
                for (int i = 0; i < n; i++) Place(_boosterButtons[i].node, P((i - (n - 1) / 2f) * spacing, 0), animated);
                _boosterHitRadius = Mathf.Min(48, spacing / 2 - 2);
                float rowY = bandMid - rowGap / 2;
                _homeBubbleNode.localPosition = P(-sideX, rowY);
                _scoreBubbleNode.localPosition = P(0, rowY);
                _restartBubbleNode.localPosition = P(sideX, rowY);
            }
            else
            {
                // Bande courte (tablette) : une rangée, boosters à gauche, contrôles à droite.
                _boosterBar.localPosition = P(0, bandMid);
                const float spacing = 88;
                for (int i = 0; i < n; i++) Place(_boosterButtons[i].node, P(-112 - (n - 1 - i) * spacing, 0), animated);
                _boosterHitRadius = Mathf.Min(48, spacing / 2 - 2);
                _homeBubbleNode.localPosition = P(92, bandMid);
                _scoreBubbleNode.localPosition = P(200, bandMid);
                _restartBubbleNode.localPosition = P(308, bandMid);
            }
        }

        static void Place(Transform node, Vector2 p, bool animated)
        {
            if (!animated) { node.localPosition = p; return; }
            Tw.Run(node.gameObject, Tw.MoveTo(node, p, 0.22f).Ease(TwEase.EaseOut), "layout");
        }

        /// Rangée haute : ⌂ réduit à gauche, carte de score au centre (réglages à droite).
        void LayoutTopRow(float y, float edgeX)
        {
            _homeBubbleNode.localScale = Vector3.one * 0.6f;
            _homeBubbleNode.localPosition = P(-edgeX, y);
            // Le pictogramme garde une taille lisible (42 pt sous l'échelle 0,6).
            _homeGlyph.localScale = Vector3.one * (42f / 28f);
            _scoreBubbleNode.localPosition = P(0, y - 22);

            if (_topRowLayout) return;
            _topRowLayout = true;
            Kill(_scoreShapeRoot);
            var cardRoot = RenderShared.Node("scoreCard", _scoreBubbleNode);
            cardRoot.SetAsFirstSibling();
            Relief.Raise(ShapeView.Rect("Card", cardRoot, Vector2.zero, new Vector2(208, 84), 32, Color.white, default, 0, 20), 5);
            _scoreLabel.Tmp.font = Fonts.Get(Fonts.AvenirHeavy);
            _scoreLabel.SizePt = 36;
            _scoreLabel.transform.localPosition = P(0, -10);
            _scoreCaption.Text = _scoreCaption.Text.ToUpper(System.Globalization.CultureInfo.CurrentCulture);
            _scoreCaption.Tmp.font = Fonts.Get(Fonts.AvenirBold);
            _scoreCaption.SizePt = 12;
            _scoreCaption.transform.localPosition = P(0, 24);
        }

        /// Bande du bas avec le score en haut : boosters (ou pastille boutique) et
        /// « recommencer » sur une seule rangée centrée.
        void LayoutBottomBand(float midY, bool animated)
        {
            bool showsRestart = _restartBubbleNode.gameObject.activeSelf;
            if (_boosterBar == null || (_boosterButtons.Count == 0 && _shopPillNode == null))
            {
                _restartBubbleNode.localPosition = P(0, midY);
                return;
            }
            _boosterBar.localPosition = P(0, midY);

            if (_boosterButtons.Count == 0)
            {
                float total = showsRestart ? 196 + 26 + 88 : 196;
                if (_shopPillNode != null) _shopPillNode.localPosition = P(-total / 2 + 98, 0);
                _restartBubbleNode.localPosition = P(total / 2 - 44, midY);
                return;
            }
            if (_shopPillNode != null) _shopPillNode.localPosition = Vector3.zero;

            const float spacing = 124;
            int slots = _boosterButtons.Count + (showsRestart ? 1 : 0);
            float SlotX(int i) => (i - (slots - 1) / 2f) * spacing;
            for (int i = 0; i < _boosterButtons.Count; i++) Place(_boosterButtons[i].node, P(SlotX(i), 0), animated);
            _boosterHitRadius = Mathf.Min(48, spacing / 2 - 2);
            _restartBubbleNode.localPosition = P(SlotX(slots - 1), midY);
        }

        // MARK: - Bouton réglages (même composant que l'accueil)

        void SetupSettingsButton()
        {
            if (Kind == GameModeKind.Demo) return;
            var logo = Look.Logo;
            _settingsBubbleNode = RenderShared.Node("settingsBtn", Root);
            var bg = ShapeView.Circle("Bg", _settingsBubbleNode, Vector2.zero, 24, logo.WithAlpha(0.10f), logo.WithAlpha(0.22f), 1, 100);
            Relief.Raise(bg, 4, surface: true);
            Glyphs.Gear(_settingsBubbleNode, Vector2.zero, 24, logo, Relief.Surface, 102);
            PlaceSettingsButton();
        }

        void PlaceSettingsButton()
        {
            if (_settingsBubbleNode == null) return;
            var g = _geometry;
            _settingsBubbleNode.localPosition = P(g.VisibleWidth / 2 - 58, g.VisibleHeight / 2 - g.TopInset - 26);
        }

        void PresentSettings()
        {
            if (_settingsOverlay != null && _settingsOverlay.IsOpen) return;
            _settingsOverlay = _app.Overlays.Settings(_app, Root, () => _app.Router.ShowTutorial());
        }
    }
}
