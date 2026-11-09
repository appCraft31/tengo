using System.Globalization;
using TenGo.Core;
using TenGo.Fx;
using TenGo.Rendering;
using TenGo.Ui;
using UnityEngine;

namespace TenGo.Game
{
    // MARK: - Mise en page (refonte 4.0, design/refonte/01_partie_A)
    //
    // En-tête : accueil (rond blanc) à gauche, score au centre (40 pt Black) et
    // « Meilleur … » dessous, pause (rond blanc, ouvre les réglages) à droite.
    // Rush : le chrono prend la place du score, le score passe dessous et une
    // barre de temps se vide. Défi du jour : pastille « Défi du jour · twist ».
    // Plus de logo ni de cartouche. Grille élargie (marges 16 pt, écart 5 pt),
    // barre des boosters libellés en bas, « Rejouer » plus petit.
    public sealed partial class GameController
    {
        const int HeaderOrder = 20;

        // Géométrie de la grille, calculée une fois sur l'écran de départ (unités de scène).
        float _cell = 84f;
        float _bubbleR = BubbleView.RadiusPt;
        float _gridCenterY;
        // En-tête
        float _rowY;
        float _rowBottom;
        TenGoText _bestLabel;
        Transform _rushBarTrack;
        ShapeView _rushBarFill;
        float _rushBarWidth;

        float CellSize => _cell;
        float GridOriginX => -(GameRules.Cols * _cell) / 2f + _cell / 2f;
        float GridOriginY => _gridCenterY - (GameRules.Rows * _cell) / 2f + _cell / 2f;
        float GridTop => GridOriginY + (GameRules.Rows - 1) * _cell;
        float GridBottom => GridOriginY;
        /// Bord bas des bulles de la dernière rangée.
        float GridEdgeBottom => GridBottom - _bubbleR;

        /// Hauteur du bloc central de l'en-tête (points de maquette).
        float HeaderBlockPt => Kind == GameModeKind.Daily || Kind == GameModeKind.Duel ? 74f : 59f;

        /// Mesures de la maquette rapportées à l'écran : en-tête sous la marge sûre,
        /// grille de la largeur visible moins 16 pt de chaque côté, bornée en
        /// hauteur pour laisser la barre des boosters.
        void ComputeLayout()
        {
            var g = _geometry;
            Design.Apply(g);
            float top = g.VisibleTop - g.TopInset;
            _rowY = top - Design.Pt(32);
            _rowBottom = _rowY - Design.Pt(Mathf.Max(32f, HeaderBlockPt / 2f));
            float gridTopEdge = _rowBottom - Design.Pt(Kind == GameModeKind.Rush ? 14 + 8 + 18 : 18);

            float gap = Design.Pt(5);
            float gridW = g.VisibleWidth - 2 * Design.Pt(Design.Gutter);
            float cellW = (gridW + gap) / GameRules.Cols;
            // Bas réservé : boosters (64 + 6 + libellé 16) + marge sûre + respiration.
            float bottom = g.VisibleBottom + BottomInsetScene + Design.Pt(Kind == GameModeKind.Normal ? 86 + 24 : 24);
            float cellH = (gridTopEdge - bottom + gap) / GameRules.Rows;
            _cell = Mathf.Min(cellW, cellH);
            _bubbleR = (_cell - gap) / 2f;
            float gridH = GameRules.Rows * _cell - gap;
            _gridCenterY = gridTopEdge - gridH / 2f;
        }

        /// Marge basse (barre de gestes, au moins 20 pt d'interface), en unités de scène.
        float BottomInsetScene => Mathf.Max(_geometry.SafeBottomPx, 20f * _geometry.PixelsPerPoint) / _geometry.PixelsPerScene;

        void SetupUI()
        {
            var ink = Design.Ink;

            // Score (ou chrono) au centre.
            _scoreBubbleNode = RenderShared.Node("scoreBubble", Root);
            if (Kind == GameModeKind.Rush)
            {
                _rushTimerLabel = Design.Text(Root, FormatClock(RushDuration), NunitoWeight.Black, Design.DisplayPt, ink,
                    new Vector2(0, _rowY + Design.Pt(9.5f)), HeaderOrder);
                _scoreLabel = Design.Text(_scoreBubbleNode, "", NunitoWeight.Bold, Design.SmallPt + 2, Design.Ink2, Vector2.zero, HeaderOrder);
                _scoreBubbleNode.localPosition = P(0, _rowY - Design.Pt(23));
                BuildRushBar();
            }
            else if (Kind == GameModeKind.Puzzle && _puzzleLevel != null)
            {
                Design.Text(Root, Loc.F("puzzle.level_label", _puzzleLevel.Index), NunitoWeight.Black, Design.TitlePt, ink,
                    new Vector2(0, _rowY + Design.Pt(11)), HeaderOrder);
                Design.Text(Root, Loc.T("puzzle.goal_clear"), NunitoWeight.Bold, Design.SmallPt, Design.Ink3,
                    new Vector2(0, _rowY - Design.Pt(16)), HeaderOrder).ShrinkToFit(Design.Pt(240));
                // Le score reste tenu (fin de partie), sans être affiché.
                _scoreLabel = Design.Text(_scoreBubbleNode, "", NunitoWeight.Black, Design.DisplayPt, ink, Vector2.zero, HeaderOrder);
                _scoreBubbleNode.localPosition = P(0, _rowY);
                _scoreBubbleNode.gameObject.SetActive(false);
            }
            else
            {
                bool chip = Kind == GameModeKind.Daily || Kind == GameModeKind.Duel;
                float numberY = _rowY + Design.Pt(HeaderBlockPt / 2f - 20f);
                _scoreLabel = Design.Text(_scoreBubbleNode, "", NunitoWeight.Black, Design.DisplayPt, ink, Vector2.zero, HeaderOrder);
                _scoreBubbleNode.localPosition = P(0, numberY);
                float subY = _rowY - Design.Pt(HeaderBlockPt / 2f - (chip ? 13f : 6.5f));
                if (chip) BuildModeChip(subY);
                else if (Kind == GameModeKind.Normal || Kind == GameModeKind.Demo)
                {
                    _bestLabel = Design.Text(Root, "", NunitoWeight.Bold, Design.SmallPt, Design.Ink3, new Vector2(0, subY), HeaderOrder);
                    RefreshBestLabel();
                }
            }
            SetScoreText(0);

            _messageLabel = Design.Text(Root, "", NunitoWeight.ExtraBold, Design.BodyPt + 2, Design.Ink2,
                new Vector2(0, GridEdgeBottom - Design.Pt(26)), 60);
            _messageLabel.gameObject.SetActive(false);

            // Accueil : rond blanc en haut à gauche.
            float edgeX = _geometry.VisibleWidth / 2f - Design.Pt(Design.Gutter + Design.IconButtonPt / 2f);
            _homeBubbleNode = Design.IconButton("homeBubble", Root, new Vector2(-edgeX, _rowY), VectorIconName.LineHome, HeaderOrder);

            // « Rejouer » : petit bouton de la barre du bas (mode normal seulement).
            _restartBubbleNode = MakeBarButton("restartBubble", VectorIconName.LineRestart, Loc.T("game_over.replay"), small: true, Root);
            _restartBubbleNode.gameObject.SetActive(Kind == GameModeKind.Normal);
        }

        /// Pastille « Défi du jour · twist » (ou « Duel ») sous le score.
        void BuildModeChip(float y)
        {
            string text;
            Color dot;
            if (Kind == GameModeKind.Daily)
            {
                var twist = _dailyToday != null ? _dailyToday.Twist.Id() : "obstacles";
                text = Loc.T("menu.daily") + " · " + Loc.T("ui.twist." + twist);
                dot = Look.BubbleColor(7);
            }
            else
            {
                text = Loc.T("menu.duel") + (string.IsNullOrEmpty(_duelCode) ? "" : " · " + _duelCode);
                dot = Look.BubbleColor(2);
            }
            var node = RenderShared.Node("modeChip", Root, P(0, y));
            var label = Design.Text(node, text, NunitoWeight.ExtraBold, 12, Design.Ink2, Vector2.zero, HeaderOrder + 2,
                TMPro.HorizontalAlignmentOptions.Left);
            float tw = label.WidthPt();
            float w = Design.Pt(12 + 8 + 6 + 12) + tw;
            Design.Pill("Bg", node, Vector2.zero, w, 26, Design.Card, HeaderOrder, hair: true);
            float x0 = -w / 2 + Design.Pt(12);
            ShapeView.Circle("Dot", node, P(x0 + Design.Pt(4), 0), Design.Pt(4), dot, default, 0, HeaderOrder + 1);
            label.transform.localPosition = P(x0 + Design.Pt(8 + 6), 0);
        }

        /// Barre du chrono du Rush : piste claire, remplissage encre qui se vide.
        void BuildRushBar()
        {
            float y = _rowBottom - Design.Pt(14 + 4);
            _rushBarWidth = _geometry.VisibleWidth - 2 * Design.Pt(Design.Gutter + 4);
            float h = Design.Pt(8);
            _rushBarTrack = RenderShared.Node("rushBar", Root, P(0, y));
            ShapeView.Rect("Track", _rushBarTrack, Vector2.zero, new Vector2(_rushBarWidth, h), h / 2, Design.Line, default, 0, HeaderOrder);
            _rushBarFill = ShapeView.Rect("Fill", _rushBarTrack, Vector2.zero, new Vector2(_rushBarWidth, h), h / 2, Design.Ink, default, 0, HeaderOrder + 1);
            UpdateRushBar();
        }

        void UpdateRushBar()
        {
            if (_rushBarFill == null) return;
            float h = Design.Pt(8);
            float f = Mathf.Clamp01((float)(_rushTimeRemaining / RushDuration));
            float w = Mathf.Max(h, _rushBarWidth * f);
            _rushBarFill.SizePt = new Vector2(w, h);
            _rushBarFill.transform.localPosition = P(-_rushBarWidth / 2 + w / 2, 0);
            _rushBarFill.gameObject.SetActive(f > 0.001f);
        }

        /// « 0:42 » (minutes et secondes, arrondi supérieur comme le chrono d'iOS).
        static string FormatClock(double seconds)
        {
            int s = Mathf.Max(0, (int)System.Math.Ceiling(seconds));
            return (s / 60) + ":" + (s % 60).ToString("00", CultureInfo.InvariantCulture);
        }

        /// Nombre groupé par milliers à la façon de la langue (« 1 240 », « 1,240 »).
        static string FormatNumber(int v)
        {
            string sep;
            try { sep = new CultureInfo(Loc.Language).NumberFormat.NumberGroupSeparator; }
            catch (CultureNotFoundException) { sep = " "; }
            // Espaces insécables (fr) : espace simple, toujours présent dans Nunito.
            if (sep == " " || sep == " " || string.IsNullOrEmpty(sep)) sep = " ";
            var nfi = new NumberFormatInfo { NumberGroupSeparator = sep, NumberGroupSizes = new[] { 3 } };
            return v.ToString("#,0", nfi);
        }

        /// Texte du score : « 1 240 » au centre, ou « 2 310 pts » sous le chrono du Rush.
        void SetScoreText(int v)
        {
            if (_scoreLabel == null) return;
            _scoreLabel.Text = Kind == GameModeKind.Rush ? FormatNumber(v) + " " + Loc.T("game.points_label") : FormatNumber(v);
        }

        void RefreshBestLabel()
        {
            if (_bestLabel == null) return;
            var best = BestFor(Kind);
            _bestLabel.Text = best is int b ? Loc.F("ui.game.best", FormatNumber(b)) : "";
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
            var node = BubbleView.Create(Root, m.Value, pos, Layers.Bubbles, radiusPt: _bubbleR);
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
                    _obstacleNodes.Add(BubbleView.MakeObstacle(Root, ScenePos(r, c), CellSize, Layers.Bubbles, _bubbleR * 2f).transform);
        }

        // MARK: - Coordonnées

        Vector2 ScenePos(int row, int col) => P(GridOriginX + col * CellSize, GridOriginY + row * CellSize);

        /// `scenePos` en points de scène.
        Vector2 ScenePt(int row, int col) => new(GridOriginX + col * CellSize, GridOriginY + row * CellSize);

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

        // MARK: - Barre du bas

        /// Bouton de la barre : carré arrondi blanc à filet, icône au trait, libellé dessous.
        Transform MakeBarButton(string name, VectorIconName icon, string label, bool small, Transform parent)
        {
            var node = RenderShared.Node(name, parent);
            FxAlpha.Of(node.gameObject);
            float side = Design.Pt(small ? 52 : 64), corner = Design.Pt(small ? 18 : 22);
            // Les petits boutons s'alignent par le bas sur les grands.
            float dy = small ? -Design.Pt(6) : 0f;
            Design.Surface("Bg", node, new Vector2(0, dy), new Vector2(side, side), corner, BarOrder);
            VectorIcon.Create(node, icon, P(0, dy), Design.Pt(small ? 26 : 28), Design.Ink, BarOrder + 2);
            Design.Text(node, label, NunitoWeight.Bold, 12, Design.Ink2, new Vector2(0, -Design.Pt(32 + 6 + 8)), BarOrder + 2)
                .ShrinkToFit(Design.Pt(82));
            return node;
        }

        /// Place les boutons de la barre du bas (boosters puis « Rejouer ») sur une rangée centrée.
        void RepositionBottomRow(bool animated = false)
        {
            var g = _geometry;
            float edgeX = g.VisibleWidth / 2f - Design.Pt(Design.Gutter + Design.IconButtonPt / 2f);
            _homeBubbleNode.localPosition = P(-edgeX, _rowY);
            // Centre des grands boutons : libellé (16 pt) et marge sûre sous eux.
            float barY = g.VisibleBottom + BottomInsetScene + Design.Pt(16 + 6 + 32);
            bool showsRestart = _restartBubbleNode.gameObject.activeSelf;
            int n = _boosterButtons.Count;
            int slots = n + (showsRestart ? 1 : 0);
            float spacing = Mathf.Min(Design.Pt(84 + 12), (g.VisibleWidth - 2 * Design.Pt(Design.Gutter)) / Mathf.Max(1, slots));
            float SlotX(int i) => (i - (slots - 1) / 2f) * spacing;
            if (_boosterBar != null)
            {
                _boosterBar.localPosition = P(0, barY);
                for (int i = 0; i < n; i++) Place(_boosterButtons[i].node, P(SlotX(i), 0), animated);
            }
            _boosterHitRadius = Mathf.Min(Design.Pt(38), spacing / 2 - 2);
            _restartBubbleNode.localPosition = P(slots > 0 ? SlotX(slots - 1) : 0, barY);
        }

        static void Place(Transform node, Vector2 p, bool animated)
        {
            if (!animated) { node.localPosition = p; return; }
            Tw.Run(node.gameObject, Tw.MoveTo(node, p, 0.22f).Ease(TwEase.EaseOut), "layout");
        }

        // MARK: - Bouton pause (ouvre les réglages)

        void SetupSettingsButton()
        {
            if (Kind == GameModeKind.Demo) return;
            _settingsBubbleNode = Design.IconButton("settingsBtn", Root, Vector2.zero, VectorIconName.LinePause, 100);
            PlaceSettingsButton();
        }

        void PlaceSettingsButton()
        {
            if (_settingsBubbleNode == null) return;
            float edgeX = _geometry.VisibleWidth / 2f - Design.Pt(Design.Gutter + Design.IconButtonPt / 2f);
            _settingsBubbleNode.localPosition = P(edgeX, _rowY);
        }

        void PresentSettings()
        {
            if (_settingsOverlay != null && _settingsOverlay.IsOpen) return;
            _settingsOverlay = _app.Overlays.Settings(_app, Root, () => _app.Router.ShowTutorial());
        }
    }
}
