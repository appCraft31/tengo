using System.Collections.Generic;
using TenGo.App;
using TenGo.Core;
using TenGo.Fx;
using TenGo.Rendering;
using TMPro;
using UnityEngine;

namespace TenGo.Screens
{
    /// Tutoriel (TutorialScene.swift). Premier lancement (TEN-4) : deux pages
    /// (relier, faire 10) qui débouchent sur une partie ; « Revoir le
    /// tutoriel » : trois pages, avec le barème. Pages qui glissent en 0,28 s.
    public sealed class TutorialScreen : ScreenBase
    {
        const int Z = 20;
        // Refonte 4.0 : fond crème et palette des bulles du thème par défaut (design/refonte/systeme.css).
        static readonly Color Background = Ui.Design.BgHex;
        static readonly Color[] BubbleColors = System.Array.ConvertAll(Ui.Design.BubblePaletteHex, h => Ui.Design.Hex(h));
        static readonly Color ButtonFill = new(0.94f, 0.91f, 0.88f);
        static readonly Color NextFill = new(0.82f, 0.95f, 0.88f);

        readonly bool _firstRun;
        int _step;
        Transform _container;
        readonly List<ShapeView> _dots = new();
        TenGoText _nextLabel;

        public int TotalSteps => _firstRun ? 2 : 3;
        public int Step => _step;
        public bool FirstRun => _firstRun;

        float VisibleHeight => Geo.VisibleHeight;

        public TutorialScreen(AppRoot app, bool firstRun) : base(app, "TutorialScreen", themedBackground: false)
        {
            _firstRun = firstRun;
            app.SetBackground(Background);
            // Refonte 4.0 : fond uni, sans les bulles floues décoratives.
            SetupChrome();
            ShowStep(0, false);
        }

        // MARK: - Chrome fixe

        void SetupChrome()
        {
            float topY = VisibleHeight / 2;
            TenGoText.Create(Root, Loc.T("tutorial.title"), Fonts.AvenirHeavy, 36, Colors.White(0.26f), Units.P(0, topY - 110), Z)
                .ShrinkToFit(UsableW - 180);

            var close = RenderShared.Node("close", Root, Units.P(Mathf.Min(750, Geo.VisibleWidth) / 2 - 60, topY - 110));
            ShapeView.Circle("Bg", close, Vector2.zero, 24, ButtonFill, Colors.White(0.68f, 0.4f), 1, Z);
            TenGoText.Create(close, "×", Fonts.AvenirMedium, 26, Colors.White(0.45f), Vector2.zero, Z + 1);
            AddTarget("close", close, new Vector2(48, 48), OnClose, animate: false);

            float dotsY = -VisibleHeight / 2 + 180;
            const float gap = 20;
            float totalW = (TotalSteps - 1) * gap;
            for (int i = 0; i < TotalSteps; i++)
                _dots.Add(ShapeView.Circle("Dot", Root, Units.P(-totalW / 2 + i * gap, dotsY), 5, Colors.White(0.72f), default, 0, Z + 1));

            float navY = -VisibleHeight / 2 + 110;
            var prev = RenderShared.Node("prev", Root, Units.P(-120, navY));
            ShapeView.Rect("Bg", prev, Vector2.zero, new Vector2(140, 52), 26, ButtonFill, Colors.White(0.68f, 0.35f), 1, Z);
            TenGoText.Create(prev, Loc.T("tutorial.previous_button"), Fonts.AvenirMedium, 17, Colors.White(0.40f), Vector2.zero, Z + 1)
                .ShrinkToFit(126);
            AddTarget("prev", prev, new Vector2(140, 52), OnPrev, animate: false);

            var next = RenderShared.Node("next", Root, Units.P(120, navY));
            ShapeView.Rect("Bg", next, Vector2.zero, new Vector2(180, 52), 26, NextFill, Colors.White(0.68f, 0.35f), 1, Z);
            _nextLabel = TenGoText.Create(next, Loc.T("tutorial.next_button"), Fonts.AvenirMedium, 17, Colors.White(0.26f), Vector2.zero, Z + 1);
            AddTarget("next", next, new Vector2(180, 52), OnNext, animate: false);
        }

        void UpdateDots()
        {
            for (int i = 0; i < _dots.Count; i++)
            {
                var dot = _dots[i];
                dot.Fill = i == _step ? Colors.White(0.30f) : Colors.White(0.72f);
                Tw.Run(dot.gameObject, Tw.Scale(dot.transform, i == _step ? 1.4f : 1f, 0.15f), "scale");
            }
        }

        void UpdateNextButton()
        {
            _nextLabel.Text = Loc.T(_step == TotalSteps - 1 ? "tutorial.play_button" : "tutorial.next_button");
            _nextLabel.ShrinkToFit(166);
        }

        // MARK: - Pages

        void ShowStep(int step, bool animated)
        {
            var old = _container;
            _container = RenderShared.Node("Step" + step, Root, Units.P(animated ? 750 : 0, 40));
            switch (step)
            {
                case 0: BuildStep0(_container); break;
                case 1: BuildStep1(_container); break;
                case 2: BuildStep2(_container); break;
            }
            UpdateDots();
            UpdateNextButton();
            if (animated)
            {
                Tw.Run(_container.gameObject, Tw.MoveTo(_container, Units.P(0, 40), 0.28f));
                if (old != null) Tw.Run(old.gameObject, Tw.Seq(Tw.MoveTo(old, Units.P(-750, 40), 0.28f), Tw.Remove(old.gameObject)));
            }
            else if (old != null) RenderShared.Destroy(old.gameObject);
        }

        static Vector2 Cell(int row, int col) => new((col - 1) * 90f, (1 - row) * 90f - 20);

        /// Page 1 : relier des bulles voisines.
        void BuildStep0(Transform parent)
        {
            AddStepTitle(Loc.T("tutorial.step0_title"), parent, 240);
            AddStepSubtitle(Loc.T("tutorial.step0_subtitle"), parent, 178);
            int[,] values = { { 3, 7, 2 }, { 4, 2, 6 }, { 1, 5, 3 }, { 8, 4, 5 } };
            int[,] colorIdx = { { 2, 4, 1 }, { 3, 1, 5 }, { 0, 3, 2 }, { 7, 3, 4 } };
            for (int row = 0; row < 4; row++)
            for (int col = 0; col < 3; col++)
            {
                var pos = Cell(row, col);
                ShapeView.Circle("Bubble", parent, Units.P(pos.x, pos.y), 36, BubbleColors[colorIdx[row, col]], default, 0, Z);
                TenGoText.Create(parent, values[row, col].ToString(), Fonts.AvenirMedium, 28, Colors.White(0.30f), Units.P(pos.x, pos.y), Z + 1);
            }
            AnimatePath(new[] { Cell(1, 0), Cell(1, 1), Cell(2, 2) }, parent);

            var sum = TenGoText.Create(parent, "4 + 2 + … = 10 ?", Fonts.AvenirMedium, 18, Colors.White(0.45f), Units.P(0, -210), Z + 1);
            var go = sum.gameObject;
            FxAlpha.Of(go).Alpha = 0;
            Tw.Run(go, Tw.Seq(Tw.Wait(1.6f), Tw.FadeIn(go, 0.4f)));
        }

        /// Page 2 : la somme fait 10.
        void BuildStep1(Transform parent)
        {
            AddStepTitle(Loc.T("tutorial.step1_title"), parent, 240);
            AddStepSubtitle(Loc.T("tutorial.step1_subtitle"), parent, 178);
            var examples = new (int[] vals, int[] colors)[]
            {
                (new[] { 1, 9 }, new[] { 0, 4 }),
                (new[] { 3, 3, 4 }, new[] { 2, 7, 3 }),
                (new[] { 2, 3, 2, 3 }, new[] { 1, 5, 2, 6 }),
            };
            float y = 100;
            foreach (var (vals, colors) in examples)
            {
                AddComboRow(vals, colors, y, parent);
                y -= 100;
            }
            TenGoText.Create(parent, Loc.T("tutorial.step1_hint"), Fonts.AvenirMedium, 16, Colors.White(0.50f), Units.P(0, -210), Z + 1).Wrap(420);
        }

        /// Page 3 (revoir seulement) : le barème.
        void BuildStep2(Transform parent)
        {
            AddStepTitle(Loc.T("tutorial.step2_title"), parent, 252);
            string pts = Loc.T("game.points_label");
            var rows = new (string left, string right)[]
            {
                (Loc.T("tutorial.step2_2_bubbles"), "10 " + pts),
                (Loc.T("tutorial.step2_3_bubbles"), "30 " + pts),
                (Loc.T("tutorial.step2_4_bubbles"), "100 " + pts),
                (Loc.T("tutorial.step2_5plus_bubbles"), "100 + 50 × extra"),
            };
            float y = 110;
            foreach (var (left, right) in rows)
            {
                var row = RenderShared.Node("ScoreRow", parent, Units.P(0, y));
                ShapeView.Rect("Bg", row, Vector2.zero, new Vector2(400, 50), 25, ButtonFill, Colors.White(0.70f, 0.30f), 1, Z);
                var r = TenGoText.Create(row, right, Fonts.AvenirMedium, 18, Colors.White(0.28f), Units.P(180, 0), Z + 1,
                    horizontal: HorizontalAlignmentOptions.Right);
                TenGoText.Create(row, left, Fonts.AvenirMedium, 18, Colors.White(0.36f), Units.P(-180, 0), Z + 1,
                    horizontal: HorizontalAlignmentOptions.Left).ShrinkToFit(Mathf.Max(80, 352 - r.WidthPt()));
                y -= 62;
            }
            var pc = RenderShared.Node("PerfectClear", parent, Units.P(0, y - 20));
            ShapeView.Rect("Bg", pc, Vector2.zero, new Vector2(400, 58), 29, new Color(0.99f, 0.95f, 0.78f), Colors.White(0.72f, 0.35f), 1, Z);
            TenGoText.Create(pc, Loc.T("tutorial.step2_perfect_clear"), Fonts.AvenirMedium, 16, new Color(0.75f, 0.52f, 0.12f), Vector2.zero, Z + 1)
                .ShrinkToFit(370);
        }

        void AddStepTitle(string text, Transform parent, float y) =>
            TenGoText.Create(parent, text, Fonts.AvenirHeavy, 26, Colors.White(0.24f), Units.P(0, y), Z + 1).Wrap(460);

        void AddStepSubtitle(string text, Transform parent, float y) =>
            TenGoText.Create(parent, text, Fonts.AvenirMedium, 18, Colors.White(0.42f), Units.P(0, y), Z + 1).Wrap(460);

        void AddComboRow(int[] values, int[] colorIdx, float y, Transform parent)
        {
            const float r = 28, gap = 70;
            float totalW = (values.Length - 1) * gap;
            float startX = -totalW / 2;
            for (int i = 0; i < values.Length; i++)
            {
                float x = startX + i * gap;
                ShapeView.Circle("Bubble", parent, Units.P(x, y), r, BubbleColors[colorIdx[i]], default, 0, Z);
                TenGoText.Create(parent, values[i].ToString(), Fonts.AvenirMedium, 22, Colors.White(0.28f), Units.P(x, y), Z + 1);
                if (i < values.Length - 1)
                    TenGoText.Create(parent, "+", Fonts.AvenirMedium, 18, Colors.White(0.55f), Units.P(x + gap / 2, y), Z + 1);
            }
            // « = 10 ✓ » : la coche n'existe dans aucune police embarquée, elle est dessinée.
            var green = new Color(0.30f, 0.68f, 0.42f);
            var result = TenGoText.Create(parent, "= 10", Fonts.AvenirMedium, 17, green, Units.P(totalW / 2 + r + 16, y), Z + 1,
                horizontal: HorizontalAlignmentOptions.Left);
            UiArt.Check(parent, Units.P(totalW / 2 + r + 16 + result.WidthPt() + 13, y), 17, green, Z + 1);
        }

        /// Chemin de démonstration : traits blancs qui s'enchaînent, anneaux de sélection.
        void AnimatePath(Vector2[] positions, Transform parent)
        {
            var lines = new List<GameObject>();
            var seq = new List<Tween> { Tw.Wait(0.5f) };
            for (int i = 1; i < positions.Length; i++)
            {
                var p0 = positions[i - 1];
                var p1 = positions[i];
                seq.Add(Tw.Call(() =>
                {
                    if (parent == null) return;
                    var line = StrokeView.Create("PathLine", parent, new[] { Units.P(p0.x, p0.y), Units.P(p1.x, p1.y) },
                        Colors.White(1, 0.75f), 7, Z + 2);
                    lines.Add(line.gameObject);
                }));
                seq.Add(Tw.Wait(0.35f));
            }
            foreach (var pos in positions)
            {
                var ring = ShapeView.Circle("Ring", parent, Units.P(pos.x, pos.y), 39, Color.clear, Colors.White(1, 0.9f), 3, Z + 3);
                var go = ring.gameObject;
                FxAlpha.Of(go).Alpha = 0;
                Tw.Run(go, Tw.Seq(Tw.Wait(0.4f), Tw.FadeIn(go, 0.2f)));
            }
            seq.Add(Tw.Wait(1.2f));
            seq.Add(Tw.Call(() => { foreach (var l in lines) RenderShared.Destroy(l); }));
            Tw.Run(parent.gameObject, Tw.Seq(seq.ToArray()));
        }

        // MARK: - Navigation

        void OnClose()
        {
            if (_firstRun)
            {
                // Au premier lancement, fermer = passer : on va jouer.
                S.Settings.HasSeenTutorial = true;
                NavigateToNewGame();
            }
            else App.ShowMenu();
        }

        void OnNext()
        {
            if (_step < TotalSteps - 1)
            {
                _step++;
                ShowStep(_step, true);
            }
            else
            {
                S.Settings.HasSeenTutorial = true;
                S.Analytics.TutorialComplete();
                NavigateToNewGame();
            }
        }

        void OnPrev()
        {
            if (_step <= 0) return;
            _step--;
            ShowStep(_step, true);
        }

        void NavigateToNewGame()
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
