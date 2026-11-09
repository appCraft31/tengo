using System.Collections.Generic;
using System.Linq;
using TenGo.Core;
using TenGo.Fx;
using TenGo.Rendering;
using TenGo.Ui;
using TMPro;
using UnityEngine;
using TrailRenderer = TenGo.Rendering.TrailRenderer;

namespace TenGo.Showcase
{
    /// Vitrine des briques de rendu de l'étape 4 (scène GlowDemo et captures
    /// `TenGo.Editor.Captures.*`) : plateau de jeu 9 × 7 avec un chemin tracé et
    /// chaque état de bulle, matières et tracés, effets, icônes et textes.
    /// Tout aléa passe par une graine fixe : les images sont reproductibles.
    public static class Vitrine
    {
        public enum Page { Grid, Materials, Effects, Ui }

        /// Surface d'une scène SpriteKit du jeu.
        public static readonly Vector2 ScenePt = new(750, 1334);
        public const float CellPt = 84f;

        /// Construit `page` sous `root` ; règle le fond de `cam`. Retourne le directeur d'effets (page Effets).
        public static JuiceDirector Build(Page page, Transform root, Camera cam)
        {
            if (cam != null) cam.backgroundColor = Look.Background;
            ThemeBackground.Make(root, Look.Theme, ScenePt, new System.Random(7));
            switch (page)
            {
                case Page.Grid: GridPage(root); return null;
                case Page.Materials: MaterialsPage(root); return null;
                case Page.Effects: return EffectsPage(root, cam);
                default: UiPage(root); return null;
            }
        }

        /// Centre de la case (ligne 0 = bas), en unités, comme `GameScene.scenePos`.
        public static Vector2 CellPos(int row, int col) =>
            Units.P(-(GameRules.Cols * CellPt) / 2f + CellPt / 2f + col * CellPt,
                    -(GameRules.Rows * CellPt) / 2f + CellPt / 2f + row * CellPt);

        /// Logo « TEN • GO » au-dessus de la grille (GameScene.setupUI).
        public static void Logo(Transform root, float y, int dotValue = 3)
        {
            var logo = Look.Logo;
            TenGoText.Create(root, "TEN", Fonts.AvenirHeavy, 48, logo, Units.P(-14, y), 30, horizontal: HorizontalAlignmentOptions.Right);
            TenGoText.Create(root, "GO", Fonts.AvenirHeavy, 48, logo, Units.P(14, y), 30, horizontal: HorizontalAlignmentOptions.Left);
            var dot = ShapeView.Circle("LogoDot", root, Units.P(0, y + 3), 11, Look.BubbleColor(dotValue), default, 0, 40);
            Tw.Run(dot.gameObject, Tw.Forever(Tw.Seq(Tw.Scale(dot.transform, 1.15f, 1.4f), Tw.Scale(dot.transform, 0.90f, 1.4f))));
        }

        // MARK: - Plateau

        /// Grille 9 × 7 d'une vraie partie (graine 42) : chemin de 4 bulles tracé
        /// avec une fleur ×3 au milieu, deux bulles gelées, deux ancrées, deux pierres.
        public static Dictionary<Cell, BubbleView> GridPage(Transform root, ulong seed = 42, bool withPath = true)
        {
            var grid = GridModel.Generate(new SeededGenerator(seed));
            float gridTop = -(GameRules.Rows * CellPt) / 2f + CellPt / 2f + (GameRules.Rows - 1) * CellPt;
            Logo(root, gridTop + 95);

            List<Cell> path = null;
            if (withPath)
                path = grid.SumTenGroups(4).Where(g => g.Count == 4).OrderBy(g => g.Min(c => c.Row)).ThenBy(g => g[0].Col).FirstOrDefault();
            var onPath = new HashSet<Cell>(path ?? new List<Cell>());
            var stones = new HashSet<Cell> { new(7, 1), new(4, 5) };
            var frozen = new HashSet<Cell> { new(6, 3), new(2, 6) };
            var anchored = new HashSet<Cell> { new(8, 5), new(1, 0) };
            stones.ExceptWith(onPath); frozen.ExceptWith(onPath); anchored.ExceptWith(onPath);

            var views = new Dictionary<Cell, BubbleView>();
            for (int r = 0; r < GameRules.Rows; r++)
            for (int c = 0; c < GameRules.Cols; c++)
            {
                var cell = new Cell(r, c);
                if (stones.Contains(cell)) { BubbleView.MakeObstacle(root, CellPos(r, c), CellPt); continue; }
                var b = grid[r, c];
                if (b == null) continue;
                var v = BubbleView.Create(root, b.Value, CellPos(r, c), Layers.Bubbles);
                if (frozen.Contains(cell)) v.SetFrozen(true);
                if (anchored.Contains(cell)) v.SetAnchored(true);
                views[cell] = v;
            }

            if (path != null)
            {
                var mid = path[path.Count / 2];
                views[mid].SetBloom(3);
                // Toutes les bulles du chemin suivent la tension courante (GameScene).
                foreach (var cell in path) views[cell].SetSelected(true, 1f);
                var pts = path.Select(c => CellPos(c.Row, c.Col)).ToList();
                var trail = RenderShared.Node("TrailLayer", root);
                TrailRenderer.Make(trail, pts, Look.Trail, Look.Accent, 1f);
                TrailRenderer.MoveComet(null, trail, pts[^1], 1f, Look.Accent);
                int points = Scoring.ForPath(path.Count) * 3;
                var head = pts[^1];
                var tier = PopTiers.ForLength(path.Count);
                TenGoText.Create(root, "+" + points, Fonts.AvenirHeavy, tier == PopTier.Small ? 30 : tier == PopTier.Medium ? 38 : 46,
                    points >= 200 ? new Color(0.92f, 0.55f, 0.14f) : Colors.White(0.28f, 0.92f), head + Units.P(0, 62), Layers.PathGain);
            }
            return views;
        }

        // MARK: - Matières, tracés, états

        static readonly BubbleMaterial[] AllMaterials =
            { BubbleMaterial.Classic, BubbleMaterial.Matte, BubbleMaterial.Glossy, BubbleMaterial.Glass, BubbleMaterial.Neon };
        static readonly TrailKind[] AllTrails = { TrailKind.Classic, TrailKind.Dotted, TrailKind.Ink, TrailKind.Ribbon, TrailKind.Neon };

        static void MaterialsPage(Transform root)
        {
            var ink = Look.Logo;
            float y = 560;
            Caption(root, "Matières", y + 50, ink);
            for (int m = 0; m < AllMaterials.Length; m++)
            {
                float rowY = y - m * 78;
                TenGoText.Create(root, AllMaterials[m].ToString().ToLowerInvariant(), NunitoWeight.Bold, 18, ink, Units.P(-270, rowY), 30,
                    horizontal: HorizontalAlignmentOptions.Left);
                for (int i = 0; i < 4; i++)
                    BubbleView.Create(root, 2 * i + 1 + m % 2, Units.P(-60 + i * 84, rowY), 0, AllMaterials[m], 34);
            }

            y = 110;
            Caption(root, "Tracés", y + 50, ink);
            for (int t = 0; t < AllTrails.Length; t++)
            {
                float rowY = y - t * 62;
                TenGoText.Create(root, AllTrails[t].ToString().ToLowerInvariant(), NunitoWeight.Bold, 18, ink, Units.P(-270, rowY), 30,
                    horizontal: HorizontalAlignmentOptions.Left);
                var pts = new List<Vector2> { Units.P(-110, rowY - 14), Units.P(-10, rowY + 14), Units.P(90, rowY - 14), Units.P(200, rowY + 10) };
                for (int k = 0; k < pts.Count; k++)
                    ShapeView.Circle("Dot", root, pts[k], 22, Look.BubbleColor(k * 2 + 1), Look.BubbleColor(k * 2 + 1).Darkened(0.12f), 1.5f, 0);
                TrailRenderer.Make(root, pts, AllTrails[t], Look.Accent, t == 0 ? 1f : 0f);
            }

            // États : sélection, gelée, ancrée, pierre, fleur, écrasement, souffle.
            y = -290;
            Caption(root, "États", y + 60, ink);
            var labels = new[] { "sélection", "gelée", "ancrée", "pierre", "fleur", "écrasée", "pop" };
            for (int i = 0; i < labels.Length; i++)
            {
                float x = -252 + i * 84;
                var pos = Units.P(x, y);
                TenGoText.Create(root, labels[i], NunitoWeight.SemiBold, 13, ink, Units.P(x, y - 54), 30);
                if (i == 3) { BubbleView.MakeObstacle(root, pos, 72); continue; }
                var b = BubbleView.Create(root, i + 2, pos, 0, null, 32);
                switch (i)
                {
                    case 0: b.SetSelected(true, 1f); break;
                    case 1: b.SetFrozen(true); break;
                    case 2: b.SetAnchored(true); break;
                    case 4: b.SetBloom(5); break;
                    case 5: b.transform.localScale = new Vector3(1.12f, 0.86f, 1); break;
                    case 6: b.transform.localScale = Vector3.one * 1.35f; FxAlpha.Of(b.gameObject).Alpha = 0.6f; break;
                }
            }
            // Fleur autonome, horloge et pousse (SeedArt).
            BubbleView.MakeFlowerVisual(root, Units.P(-200, -470), 4);
            SeedArt.Flower(root, Units.P(-80, -470), 60, 10);
            SeedArt.Sprout(root, Units.P(30, -470), 60, 10);
            SeedArt.Clock(root, Units.P(130, -470), 40, ink, 10);
            SeedArt.PowerBadge(root, Units.P(240, -470), 3, 10);
        }

        static void Caption(Transform root, string text, float y, Color ink) =>
            TenGoText.Create(root, text, Fonts.AvenirHeavy, 26, ink, Units.P(-300, y), 30, horizontal: HorizontalAlignmentOptions.Left);

        // MARK: - Effets

        /// Plateau sans tracé, trois éclatements (petit, moyen, grand), secousse,
        /// flash et célébration de victoire, lancés à t = 0.
        static JuiceDirector EffectsPage(Transform root, Camera cam)
        {
            var views = GridPage(root, 42, withPath: false);
            var fx = RenderShared.Node("FxLayer", root);
            var shake = cam != null ? ShakeCamera.Attach(cam) : null;
            var juice = new JuiceDirector(fx, shake, ScenePt, new System.Random(3));
            var targets = new[] { (new Cell(6, 1), PopTier.Small), (new Cell(4, 3), PopTier.Medium), (new Cell(2, 5), PopTier.Large) };
            foreach (var (cell, tier) in targets)
            {
                if (!views.TryGetValue(cell, out var v)) continue;
                juice.Burst(CellPos(cell.Row, cell.Col), Look.BubbleColor(v.Value), tier);
                v.PlayPop();
                // Voisines poussées vers l'extérieur.
                foreach (var n in new[] { new Cell(cell.Row, cell.Col + 1), new Cell(cell.Row + 1, cell.Col) })
                    if (views.TryGetValue(n, out var nv)) nv.Nudge((n.Col - cell.Col) * tier.NeighborPush(), (n.Row - cell.Row) * tier.NeighborPush());
            }
            juice.OnCommit(PopTier.Large, 6);
            juice.OnWin(Vector2.zero, Look.Accent);
            return juice;
        }

        // MARK: - Icônes, boutons, textes

        static void UiPage(Transform root)
        {
            var ink = Look.Logo;
            Caption(root, "Icônes", 600, ink);
            var names = (VectorIconName[])System.Enum.GetValues(typeof(VectorIconName));
            for (int i = 0; i < names.Length; i++)
            {
                float x = -270 + (i % 7) * 90, y = 520 - (i / 7) * 110;
                VectorIcon.Create(root, names[i], Units.P(x, y), 54, Colors.FromRgb(new RgbColor(0x5A / 255.0, 0x55 / 255.0, 0x50 / 255.0)), 10);
                TenGoText.Create(root, names[i].ToString().Replace("Icon", "").Replace("Booster", "B."), NunitoWeight.SemiBold, 12, ink, Units.P(x, y - 44), 10);
            }
            // Grande icône : netteté du champ de distance.
            VectorIcon.Create(root, VectorIconName.IconFlame, Units.P(230, -20), 100, Look.Accent.Darkened(0.2f), 10);

            Caption(root, "Boutons", 300, ink);
            UiButton.Create(root, "Primary", Units.P(-130, 230), new Vector2(240, 64), 32, Look.Accent, "Nouvelle partie",
                Fonts.AvenirDemiBold, 22, Look.Accent.ReadableInk(), 20, null);
            UiButton.Create(root, "Secondary", Units.P(150, 230), new Vector2(200, 58), 29, null, "Continuer",
                Fonts.AvenirDemiBold, 20, ink, 20, null);
            UiButton.CreateRound(root, "Home", Units.P(-230, 120), 40, Color.white, VectorIconName.IconPlay, 34, ink, 20, null);
            UiButton.CreateRound(root, "Hint", Units.P(-120, 120), 40, Color.white, VectorIconName.BoosterHint, 34, ink, 20, null);
            CoinIcon.Make(root, Units.P(0, 120), 22, 20);
            TenGoText.Create(root, "1 240", NunitoWeight.Black, 30, ink, Units.P(36, 120), 20, horizontal: HorizontalAlignmentOptions.Left);
            var chipBg = ShapeView.Rect("Tile", root, Units.P(230, 120), new Vector2(120, 100), 22, Look.BubbleColor(5), default, 0, 20);
            Relief.Raise(chipBg, 6);
            Relief.IconChip(root, Units.P(230, 120), 32, Look.BubbleColor(5), 22);
            VectorIcon.Create(root, VectorIconName.IconPuzzle, Units.P(230, 120), 40, ink, 24);

            Caption(root, "Textes", -10, ink);
            var weights = (NunitoWeight[])System.Enum.GetValues(typeof(NunitoWeight));
            for (int i = 0; i < weights.Length; i++)
                TenGoText.Create(root, $"Nunito {weights[i]} 0123456789", weights[i], 26, ink, Units.P(-300, -70 - i * 40), 10,
                    horizontal: HorizontalAlignmentOptions.Left);
            // Textes réels du catalogue (les replis sont réduits à ses caractères).
            string[] cjk = { "壊すバブルをタップ · デイリーチャレンジ", "없앨 방울을 탭하세요 · 데일리 챌린지", "点击要消除的泡泡 · 完成每日挑战" };
            for (int i = 0; i < cjk.Length; i++)
                TenGoText.Create(root, cjk[i], NunitoWeight.Bold, 26, ink, Units.P(-300, -290 - i * 46), 10,
                    horizontal: HorizontalAlignmentOptions.Left);
            TenGoText.Create(root, "Français · Deutsch ÄÖÜß · Español ñ · Português ã", NunitoWeight.SemiBold, 20, ink, Units.P(-300, -440), 10,
                horizontal: HorizontalAlignmentOptions.Left);
        }
    }
}
