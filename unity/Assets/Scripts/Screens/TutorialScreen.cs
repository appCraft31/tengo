using System.Collections.Generic;
using TenGo.App;
using TenGo.Core;
using TenGo.Fx;
using TenGo.Haptics;
using TenGo.Rendering;
using TenGo.Ui;
using TMPro;
using UnityEngine;

namespace TenGo.Screens
{
    /// Tutoriel jouable (refonte 4.0, `design/refonte/07_tutoriel`) : un mini
    /// niveau en trois étapes (`TutorialLevel`) que le joueur résout lui-même,
    /// au premier lancement comme depuis « Revoir le tutoriel ».
    /// - 1 « Fais 10 » : grille 3 × 3, seules 4 et 6 sont vives, une main
    ///   fantôme montre le geste en boucle ;
    /// - 2 « Va plus loin » : grille 4 × 4, la pastille de somme suit le tracé
    ///   en direct et passe au vert à 10 ;
    /// - 3 « Ça tombe ! » : la chaîne disparaît, les bulles tombent, de
    ///   nouvelles arrivent, le joueur trouve un 10 seul ;
    /// - fin « Tu as compris ! » : cadeau de bienvenue (`WelcomeGift`, versé
    ///   une seule fois), puis la partie démarre directement.
    /// « Passer » (toutes les étapes) mène à la fin et verse aussi le cadeau.
    ///
    /// Cibles : « skip » (alias « close »), « play » (fin). Analytics :
    /// `tutorial_complete` (mini niveau mené au bout), `tutorial_skip` (step),
    /// `welcome_gift` (via).
    public sealed class TutorialScreen : ScreenBase
    {
        const int Z = 20;

        readonly bool _firstRun;
        readonly TutorialLevel _level = new();
        Transform _stage;
        Transform _header;
        readonly Dictionary<(int, int), BubbleView> _bubbles = new();
        readonly List<(int Row, int Col)> _path = new();
        Transform _trail;
        Transform _sumPill;
        Transform _hand;
        float _cell;
        Vector2 _gridCenter;
        bool _tracing;
        bool _busy;

        public int TotalSteps => TutorialLevel.StepCount;
        /// Étape affichée : 0 à 2, puis 3 pour la fin.
        public int Step => _level.Step;
        public bool FirstRun => _firstRun;
        public bool IsFinished => _level.IsFinished;
        public TutorialLevel Level => _level;
        /// Cadeau de bienvenue versé par CE passage du tutoriel.
        public bool GiftPaidNow { get; private set; }
        /// Chaîne en cours (tests).
        public IReadOnlyList<(int Row, int Col)> Path => _path;

        static float Pt(float v) => Design.Pt(v);

        /// Centre d'une bulle en points de scène (tests du geste réel).
        public Vector2? BubblePoint(int row, int col) =>
            _bubbles.TryGetValue((row, col), out var b) ? (Vector2)b.transform.position * Units.PointsPerUnit : null;

        public TutorialScreen(AppRoot app, bool firstRun) : base(app, "TutorialScreen")
        {
            _firstRun = firstRun;
            TapOnRelease = true;
            ShowStep(false);
        }

        // MARK: - Mise en page

        void ShowStep(bool animated)
        {
            if (_stage != null) RenderShared.Destroy(_stage.gameObject);
            if (_header != null) RenderShared.Destroy(_header.gameObject);
            Targets.Clear();
            _bubbles.Clear();
            _path.Clear();
            _trail = null;
            _sumPill = null;
            _hand = null;
            _stage = RenderShared.Node("Stage", Root);
            _header = RenderShared.Node("Header", Root);
            BuildHeader();
            if (_level.IsFinished) BuildEnd();
            else BuildPuzzle();
            if (animated)
            {
                FxAlpha.Of(_stage.gameObject).Alpha = 0;
                Tw.Run(_stage.gameObject, Tw.FadeIn(_stage.gameObject, 0.25f), "fade");
            }
        }

        /// Points de progression (fait = accent, courant = pilule d'encre) et « Passer ».
        void BuildHeader()
        {
            float cw = ContentW, y = HeaderRowY;
            float x = -cw / 2;
            for (int i = 0; i < TotalSteps; i++)
            {
                bool current = i == _level.Step, done = i < _level.Step;
                float w = current ? Pt(24) : Pt(8);
                ShapeView.Rect("Dot", _header, Units.P(x + w / 2, y), new Vector2(w, Pt(8)), Pt(4),
                    current ? Design.Ink : done ? Design.Accent : Design.Line, default, 0, Z + 2);
                x += w + Pt(6);
            }
            if (_level.IsFinished) return;
            var skip = Design.Text(_header, Loc.T("ui.tuto.skip"), NunitoWeight.ExtraBold, 15, Design.Ink2, new Vector2(cw / 2, y), Z + 2,
                HorizontalAlignmentOptions.Right);
            var node = RenderShared.Node("skip", _header, Units.P(cw / 2 - skip.WidthPt() / 2, y));
            var size = new Vector2(skip.WidthPt() + Pt(24), Pt(44));
            AddTarget("skip", node, size, Skip, animate: false);
            AddTarget("close", node, size, Skip, animate: false);
        }

        void BuildPuzzle()
        {
            int step = _level.Step;
            float top = HeaderBottom - Pt(56);
            var title = Design.Text(_stage, Loc.T("ui.tuto.step" + step + "_title"), NunitoWeight.Black, 26, Design.Ink, new Vector2(0, top), Z);
            title.ShrinkToFit(ContentW);
            var sub = Design.Text(_stage, Loc.T("ui.tuto.step" + step + "_sub"), NunitoWeight.Bold, 16, Design.Ink2, Vector2.zero, Z).Wrap(ContentW - Pt(40));
            float subTop = top - Pt(30);
            sub.transform.localPosition = Units.P(0, subTop - sub.HeightPt() / 2);

            int n = _level.Size;
            _cell = Mathf.Min(ContentW / 4f, Pt(88));
            float gridTop = subTop - Mathf.Max(sub.HeightPt(), Pt(44)) - Pt(30);
            _gridCenter = new Vector2(0, gridTop - _cell * n / 2);
            for (int r = 0; r < n; r++)
                for (int c = 0; c < n; c++)
                    AddBubble(r, c, false);

            float below = _gridCenter.y - _cell * n / 2 - Pt(40);
            if (step == 0)
            {
                UpdateSumPill(below);
                StartGhostHand(TutorialLevel.Step0Demo);
            }
            else if (step == 1) UpdateSumPill(below);
            else
            {
                var pill = RenderShared.Node("YourTurn", _stage, Units.P(0, below));
                var l = Design.Text(pill, Loc.T("ui.tuto.your_turn"), NunitoWeight.Black, 18, Design.Ink2, Vector2.zero, Z + 2);
                Design.Surface("Bg", pill, Vector2.zero, new Vector2(l.WidthPt() + Pt(40), Pt(48)), Pt(24), Z + 1);
            }
            _sumY = below;
        }

        float _sumY;

        Vector2 CellPos(int r, int c) =>
            new(_gridCenter.x + (c - (_level.Size - 1) / 2f) * _cell, _gridCenter.y - (r - (_level.Size - 1) / 2f) * _cell);

        BubbleView AddBubble(int r, int c, bool fall)
        {
            var pos = CellPos(r, c);
            var b = BubbleView.Create(_stage, _level.Value(r, c), Units.P(pos.x, pos.y), Z + 4, null, _cell * 0.45f);
            // Étape 1 : seules les bulles utiles sont vives.
            if (!_level.IsActive(r, c)) FxAlpha.Of(b.gameObject).Alpha = 0.35f;
            _bubbles[(r, c)] = b;
            return b;
        }

        /// Pastille de somme : mini bulles du tracé, « = somme », verte avec une coche à 10.
        void UpdateSumPill(float y)
        {
            if (_sumPill != null) RenderShared.Destroy(_sumPill.gameObject);
            if (_level.Step > 1 || _level.IsFinished) { _sumPill = null; return; }
            _sumPill = RenderShared.Node("SumPill", _stage, Units.P(0, y));
            int sum = 0;
            var values = new List<int>();
            foreach (var c in _path) { values.Add(_level.Value(c.Row, c.Col)); sum += values[values.Count - 1]; }
            bool ten = sum == 10 && _path.Count >= (_level.Step == 1 ? 3 : 2);
            var ink = ten ? Design.Hex(0x2F7A52) : Design.Ink;
            float mini = 28, gap = Pt(8);
            var parts = new List<(float w, System.Action<float> draw)>();
            for (int i = 0; i < values.Count; i++)
            {
                int v = values[i];
                if (i > 0)
                {
                    var plus = Design.Text(_sumPill, "+", NunitoWeight.Black, 16, ink, Vector2.zero, Z + 3);
                    float pw = plus.WidthPt();
                    parts.Add((pw, x => plus.transform.localPosition = Units.P(x + pw / 2, 0)));
                }
                parts.Add((Pt(mini), x => Design.MiniBubble(_sumPill, new Vector2(x + Pt(mini / 2), 0), mini, v, Z + 3)));
            }
            // Rien de tracé : « ? + ? = 10 », le but en clair.
            if (values.Count == 0)
            {
                ink = Design.Ink3;
                for (int i = 0; i < 2; i++)
                {
                    if (i > 0)
                    {
                        var plus = Design.Text(_sumPill, "+", NunitoWeight.Black, 16, ink, Vector2.zero, Z + 3);
                        float pw = plus.WidthPt();
                        parts.Add((pw, x => plus.transform.localPosition = Units.P(x + pw / 2, 0)));
                    }
                    parts.Add((Pt(mini), x => Design.MiniBubble(_sumPill, new Vector2(x + Pt(mini / 2), 0), mini, 0, Z + 3)));
                }
            }
            var eq = Design.Text(_sumPill, "= " + (values.Count == 0 ? 10 : sum), NunitoWeight.Black, 18, ink, Vector2.zero, Z + 3,
                HorizontalAlignmentOptions.Left);
            float ew = eq.WidthPt();
            parts.Add((ew, x => eq.transform.localPosition = Units.P(x, 0)));
            if (ten) parts.Add((Pt(20), x => VectorIcon.Create(_sumPill, VectorIconName.LineCheck, Units.P(x + Pt(10), 0), Pt(20), ink, Z + 3)));
            float total = -gap;
            foreach (var p in parts) total += p.w + gap;
            float cx = -total / 2;
            foreach (var p in parts) { p.draw(cx); cx += p.w + gap; }
            var fill = ten ? Design.Hex(0xE3F2EA) : Design.Card;
            Design.Surface("Bg", _sumPill, Vector2.zero, new Vector2(Mathf.Max(Pt(100), total + Pt(36)), Pt(48)), Pt(24), Z + 1, fill, hair: !ten);
        }

        /// Main fantôme : un disque translucide qui refait le geste en boucle.
        void StartGhostHand((int Row, int Col)[] demo)
        {
            var start = CellPos(demo[0].Row, demo[0].Col);
            _hand = RenderShared.Node("GhostHand", _stage, Units.P(start.x + Pt(8), start.y - Pt(14)));
            ShapeView.Circle("Halo", _hand, Vector2.zero, Pt(24), Design.Ink.WithAlpha(0.10f), default, 0, Z + 9);
            ShapeView.Circle("Tip", _hand, Vector2.zero, Pt(15), Design.Ink.WithAlpha(0.22f), default, 0, Z + 10);
            if (!JuiceSettings.MotionEnabled) return;
            var steps = new List<Tween>();
            steps.Add(Tw.Wait(0.5f));
            for (int i = 1; i < demo.Length; i++)
            {
                var p = CellPos(demo[i].Row, demo[i].Col);
                steps.Add(Tw.MoveTo(_hand, Units.P(p.x + Pt(8), p.y - Pt(14)), 0.7f).Ease(TwEase.EaseInEaseOut));
            }
            steps.Add(Tw.Wait(0.5f));
            steps.Add(Tw.Call(() => { if (_hand != null) _hand.localPosition = Units.P(start.x + Pt(8), start.y - Pt(14)); }));
            Tw.Run(_hand.gameObject, Tw.Forever(Tw.Seq(steps.ToArray())), "ghost");
        }

        void BuildEnd()
        {
            float y = Pt(80);
            Design.Text(_stage, "10", NunitoWeight.Black, 96, Design.Ink, new Vector2(0, y), Z);
            y -= Pt(84);
            Design.Text(_stage, Loc.T("ui.tuto.done_title"), NunitoWeight.Black, 26, Design.Ink, new Vector2(0, y), Z).ShrinkToFit(ContentW);
            var sub = Design.Text(_stage, Loc.T("ui.tuto.done_sub"), NunitoWeight.Bold, 16, Design.Ink2, Vector2.zero, Z).Wrap(ContentW - Pt(24));
            sub.transform.localPosition = Units.P(0, y - Pt(28) - sub.HeightPt() / 2);
            y -= Pt(28) + sub.HeightPt() + Pt(40);

            if (GiftPaidNow)
            {
                // Cadeau : « +50 » et « Indice ×2 », côte à côte.
                var coins = RenderShared.Node("GiftCoins", _stage);
                var cl = Design.Text(coins, "+" + WelcomeGift.Coins, NunitoWeight.Black, 17, Design.Ink, Vector2.zero, Z + 3, HorizontalAlignmentOptions.Left);
                float cw = Pt(20 + 8) + cl.WidthPt() + Pt(32);
                var hints = RenderShared.Node("GiftHints", _stage);
                var hl = Design.Text(hints, Loc.F("ui.tuto.hints", WelcomeGift.Hints), NunitoWeight.Black, 17, Design.Ink, Vector2.zero, Z + 3,
                    HorizontalAlignmentOptions.Left);
                float hw = Pt(20 + 8) + hl.WidthPt() + Pt(32);
                float gap = Pt(10), total = cw + gap + hw;
                coins.localPosition = Units.P(-total / 2 + cw / 2, y);
                hints.localPosition = Units.P(total / 2 - hw / 2, y);
                Design.Surface("Bg", coins, Vector2.zero, new Vector2(cw, Pt(48)), Pt(24), Z + 1);
                Design.Coin(coins, new Vector2(-cw / 2 + Pt(16 + 10), 0), 20, Z + 2);
                cl.transform.localPosition = Units.P(-cw / 2 + Pt(16 + 28), 0);
                Design.Surface("Bg", hints, Vector2.zero, new Vector2(hw, Pt(48)), Pt(24), Z + 1);
                VectorIcon.Create(hints, VectorIconName.LineBulb, Units.P(-hw / 2 + Pt(16 + 10), 0), Pt(20), Design.Ink, Z + 2);
                hl.transform.localPosition = Units.P(-hw / 2 + Pt(16 + 28), 0);
                if (JuiceSettings.MotionEnabled)
                    foreach (var t in new[] { coins, hints })
                    {
                        t.localScale = Vector3.one * 0.6f;
                        Tw.Run(t.gameObject, Tw.Scale(t, 1f, 0.3f).Ease(TwEase.EaseOut), "pop");
                    }
            }

            float cw2 = ContentW;
            float by = -HalfH + BottomInset + Pt(12 + 28);
            string label = _firstRun ? Loc.T("ui.tuto.first_game") : Loc.T("tutorial.play_button");
            var play = Design.Button("play", _stage, new Vector2(0, by), cw2, label, true, Z, VectorIconName.LinePlay);
            AddTarget("play", play, new Vector2(cw2, Pt(56)), StartFirstGame);
            AddTarget("next", play, new Vector2(cw2, Pt(56)), StartFirstGame);
        }

        // MARK: - Tracé

        (int, int)? CellAt(Vector2 pt)
        {
            if (_level.IsFinished || _bubbles.Count == 0) return null;
            float hit = _cell * 0.42f;
            foreach (var kv in _bubbles)
            {
                var c = (Vector2)kv.Value.transform.position * Units.PointsPerUnit;
                if ((pt - c).sqrMagnitude <= hit * hit) return kv.Key;
            }
            return null;
        }

        public override void PointerDown(Vector2 pt)
        {
            if (!_busy && CellAt(pt) is (int, int) cell && _level.IsActive(cell.Item1, cell.Item2))
            {
                _tracing = true;
                StopGhost();
                _path.Clear();
                Extend(cell);
                return;
            }
            base.PointerDown(pt);
        }

        public override void PointerMove(Vector2 pt)
        {
            if (_tracing)
            {
                if (CellAt(pt) is (int, int) cell) Extend(cell);
                return;
            }
            base.PointerMove(pt);
        }

        public override void PointerUp(Vector2 pt)
        {
            if (_tracing) { _tracing = false; Release(); return; }
            base.PointerUp(pt);
        }

        public override void PointerCancel()
        {
            if (_tracing) { _tracing = false; ClearPath(); return; }
            base.PointerCancel();
        }

        /// Ajoute une case au tracé (retour en arrière en repassant sur l'avant-dernière).
        void Extend((int Row, int Col) cell)
        {
            if (_path.Count > 0 && _path[_path.Count - 1] == cell) return;
            if (_path.Count > 1 && _path[_path.Count - 2] == cell)
            {
                var last = _path[_path.Count - 1];
                _path.RemoveAt(_path.Count - 1);
                _bubbles[last].SetSelected(false);
                Redraw();
                return;
            }
            var candidate = new List<(int, int)>(_path) { cell };
            if (!_level.IsValidPath(candidate)) return;
            _path.Add(cell);
            _bubbles[cell].SetSelected(true);
            HapticManager.Light();
            Redraw();
        }

        void Redraw()
        {
            var pts = new List<Vector2>();
            foreach (var c in _path) { var p = CellPos(c.Row, c.Col); pts.Add(Units.P(p.x, p.y)); }
            if (_trail != null) { RenderShared.Destroy(_trail.gameObject); _trail = null; }
            if (pts.Count >= 2) _trail = Rendering.TrailRenderer.Make(_stage, pts, Look.Trail, Look.Accent, 0, Z + 8);
            UpdateSumPill(_sumY);
        }

        void ClearPath()
        {
            foreach (var c in _path) if (_bubbles.TryGetValue(c, out var b)) b.SetSelected(false);
            _path.Clear();
            Redraw();
        }

        void Release()
        {
            if (_path.Count == 0) return;
            var chain = new List<(int, int)>(_path);
            if (!_level.Accepts(chain))
            {
                // Refus : petit tremblement, et rappel « au moins 3 bulles » à l'étape 2.
                foreach (var c in chain) _bubbles[c].Nudge(Pt(4), 0);
                if (_level.Step == 1 && _level.Sum(chain) == 10) ShowNote(Loc.T("ui.tuto.longer"));
                ClearPath();
                if (_level.Step == 0) StartGhostHand(TutorialLevel.Step0Demo);
                return;
            }
            Validate(chain);
        }

        /// Trace une chaîne d'un coup (tests) ; vrai si elle a fait passer l'étape.
        public bool Trace(params (int Row, int Col)[] cells)
        {
            if (_busy || _level.IsFinished) return false;
            ClearPath();
            foreach (var c in cells) Extend(c);
            if (_path.Count != cells.Length) { ClearPath(); return false; }
            int before = _level.Step;
            Release();
            return _level.Step > before;
        }

        void Validate(List<(int Row, int Col)> chain)
        {
            int stepBefore = _level.Step;
            _level.Submit(chain);
            HapticManager.Medium();
            _busy = true;
            _path.Clear();
            if (_trail != null) Redraw();

            // Pop des bulles de la chaîne, « +N » au-dessus.
            var mid = Vector2.zero;
            foreach (var c in chain) mid += CellPos(c.Row, c.Col);
            mid /= chain.Count;
            foreach (var c in chain)
            {
                var b = _bubbles[c];
                _bubbles.Remove(c);
                b.PlayPop(() => { if (b != null) RenderShared.Destroy(b.gameObject); });
            }
            ShowScorePop(mid, chain.Count * 10);

            if (stepBefore == 1)
            {
                // Gravité : les bulles restantes tombent, les nouvelles arrivent d'en haut.
                FxTime.AfterRealtime(0.35f, () =>
                {
                    if (_stage == null) return;
                    ShowStep(false);
                    AnimateFall();
                    _busy = false;
                });
                return;
            }
            if (_level.IsFinished)
            {
                S.Analytics.TutorialComplete();
                Finish("complete");
            }
            FxTime.AfterRealtime(0.45f, () =>
            {
                if (_stage == null) return;
                _busy = false;
                ShowStep(true);
            });
        }

        void AnimateFall()
        {
            var from = _level.FallFrom;
            if (from == null || !JuiceSettings.MotionEnabled) return;
            foreach (var kv in _bubbles)
            {
                var (r, c) = kv.Key;
                int origin = from[r, c];
                if (origin == r) continue;
                var target = kv.Value.transform.localPosition;
                var start = CellPos(origin, c);
                kv.Value.transform.localPosition = Units.P(start.x, start.y);
                Tw.Run(kv.Value.gameObject, Tw.MoveTo(kv.Value.transform, target, 0.35f + 0.05f * (r - origin)).Ease(TwEase.EaseIn), "fall");
                if (origin < 0)
                {
                    FxAlpha.Of(kv.Value.gameObject).Alpha = 0;
                    Tw.Run(kv.Value.gameObject, Tw.FadeIn(kv.Value.gameObject, 0.25f), "in");
                }
            }
        }

        void ShowScorePop(Vector2 at, int points)
        {
            var node = RenderShared.Node("ScorePop", Root, Units.P(at.x, at.y + _cell * 0.6f));
            var l = Design.Text(node, "+" + points, NunitoWeight.Black, 20, Design.Ink, Vector2.zero, Z + 21);
            Design.Surface("Bg", node, Vector2.zero, new Vector2(l.WidthPt() + Pt(24), Pt(38)), Pt(14), Z + 20);
            var go = node.gameObject;
            Tw.Run(go, Tw.Seq(Tw.MoveBy(node, Units.P(0, Pt(24)), 0.6f), Tw.FadeOut(go, 0.3f), Tw.Remove(go)));
        }

        void ShowNote(string text)
        {
            var node = RenderShared.Node("Note", Root, Units.P(0, _sumY - Pt(52)));
            Design.Text(node, text, NunitoWeight.ExtraBold, 14, Design.Ink2, Vector2.zero, Z + 5).ShrinkToFit(ContentW);
            var go = node.gameObject;
            Tw.Run(go, Tw.Seq(Tw.Wait(1.6f), Tw.FadeOut(go, 0.3f), Tw.Remove(go)));
        }

        void StopGhost()
        {
            if (_hand == null) return;
            RenderShared.Destroy(_hand.gameObject);
            _hand = null;
        }

        // MARK: - Fin, cadeau et partie

        /// « Passer » : à la fin tout de suite, cadeau compris (personne n'est pénalisé).
        void Skip()
        {
            if (_level.IsFinished) return;
            S.Analytics.TutorialSkip(_level.Step);
            _level.Skip();
            _busy = false;
            _tracing = false;
            Finish("skip");
            ShowStep(true);
        }

        /// Fin du mini niveau (complété ou passé) : tutoriel vu, cadeau s'il n'a jamais été versé.
        void Finish(string via)
        {
            S.Settings.HasSeenTutorial = true;
            if (S.WelcomeGift.PayIfNeeded())
            {
                GiftPaidNow = true;
                S.Analytics.WelcomeGift(via);
            }
            App.FlushSave();
        }

        void StartFirstGame()
        {
            S.Games.Clear();
            App.FlushSave();
            App.StartGame(GameLaunch.Normal());
        }

        public override void Back()
        {
            if (_firstRun) return;
            App.ShowMenu();
        }
    }
}
