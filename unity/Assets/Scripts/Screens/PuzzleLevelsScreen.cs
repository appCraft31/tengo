using System.Linq;
using TenGo.App;
using TenGo.Core;
using TenGo.Rendering;
using TMPro;
using UnityEngine;

namespace TenGo.Screens
{
    /// Sélection des niveaux d'un monde (PuzzleLevelsScene.swift) : grille de
    /// pastilles, étoiles obtenues, progression du monde. Un niveau se
    /// déverrouille dès que le précédent a été résolu ; un niveau résolu reste
    /// rejouable pour améliorer ses étoiles.
    ///
    /// Cibles : « back », « level_&lt;i&gt; » (pastilles jouables), « playNext »
    /// (bouton du prochain niveau à résoudre).
    public sealed class PuzzleLevelsScreen : ScreenBase
    {
        const int Z = 20;
        readonly int _world;

        public int World => _world;
        /// Prochain niveau à résoudre (premier déverrouillé sans étoile), 0 s'il n'y en a pas.
        public int NextLevelIndex { get; private set; }

        public PuzzleLevelsScreen(AppRoot app, int world) : base(app, "PuzzleLevelsScreen")
        {
            _world = world;
            Build();
        }

        static Vector2 P(float x, float y) => Units.P(x, y);

        void Build()
        {
            var theme = Look.Theme;
            float usableW = UsableW;
            float halfH = HalfH;
            float bottomY = -halfH + BottomInset;
            float contentW = Mathf.Min(usableW - 48, 600);

            var levels = S.Catalog.Levels(_world);
            var progress = S.Puzzles;
            int completed = progress.CompletedCount(_world);
            int stars = progress.TotalStars(_world);

            // En-tête : retour à gauche, nom du monde, étoiles à droite.
            float headerY = halfH - TopInset - 36;
            AddBackButton(new Vector2(-contentW / 2 + 30, headerY));

            var gold = new Color(0.45f, 0.34f, 0.10f);
            var starsLabel = TenGoText.Create(Root, $"{stars} / {levels.Count * 3}", Fonts.AvenirBold, 17, gold, Vector2.zero, Z + 2,
                horizontal: HorizontalAlignmentOptions.Left);
            float starW = 15f, starGap = 6f;
            float labelW = starsLabel.WidthPt();
            float pillW = starW + starGap + labelW + 34;
            float pillX = contentW / 2 - pillW / 2;
            var pill = ShapeView.Rect("StarsPill", Root, P(pillX, headerY), new Vector2(pillW, 42), 21, new Color(0.99f, 0.95f, 0.80f), default, 0, Z);
            Relief.Raise(pill, 3);
            float startX = pillX - (starW + starGap + labelW) / 2;
            Glyphs.Star(Root, P(startX + starW / 2, headerY + 1), 17 * 1.05f, gold, true, Z + 2);
            starsLabel.transform.localPosition = P(startX + starW + starGap, headerY);

            var title = TenGoText.Create(Root, Loc.T("menu.puzzles"), Fonts.AvenirHeavy, 34, Look.Logo, P(-contentW / 2 + 74, headerY), Z + 2,
                horizontal: HorizontalAlignmentOptions.Left);
            title.ShrinkToFit(contentW - 74 - pillW - 14);

            // Carte du monde : nom, avancement et barre.
            const float cardH = 104;
            float cardY = headerY - 42 - cardH / 2;
            AddWorldCard(cardY, contentW, cardH, completed, levels.Count);

            // Niveau à jouer : le premier déverrouillé pas encore résolu.
            var next = levels.FirstOrDefault(l => progress.IsUnlocked(l.World, l.Index) && progress.Stars(l.World, l.Index) == 0);
            NextLevelIndex = next?.Index ?? 0;
            const float ctaH = 70;
            float ctaY = bottomY + 18 + ctaH / 2;
            if (next != null) AddPlayButton(next, ctaY, contentW, ctaH);

            // Grille de pastilles : 4 colonnes, tassées si l'écran est court (iPad).
            const int perRow = 4;
            int rows = Mathf.CeilToInt(levels.Count / (float)perRow);
            const float gap = 18;
            float gridTop = cardY - cardH / 2 - 28;
            float gridBottom = (next == null ? bottomY : ctaY + ctaH / 2) + 22;
            float byWidth = (contentW - gap * (perRow - 1)) / perRow;
            float byHeight = (gridTop - gridBottom - gap * Mathf.Max(0, rows - 1)) / Mathf.Max(1, rows);
            float cell = Mathf.Min(byWidth, byHeight);
            float gridW = cell * perRow + gap * (perRow - 1);
            for (int offset = 0; offset < levels.Count; offset++)
            {
                var level = levels[offset];
                int column = offset % perRow, row = offset / perRow;
                float x = -gridW / 2 + cell / 2 + column * (cell + gap);
                float y = gridTop - cell / 2 - row * (cell + gap);
                AddLevelTile(level, new Vector2(x, y), cell, next != null && level.Index == next.Index, offset);
            }
        }

        void AddWorldCard(float y, float width, float height, int completed, int total)
        {
            var tint = Look.BubbleColor(9);
            var ink = tint.ReadableInk();
            var card = RenderShared.Node("WorldCard", Root, P(0, y));
            var bg = ShapeView.Rect("Bg", card, Vector2.zero, new Vector2(width, height), 28, tint, default, 0, Z);
            Relief.Raise(bg, 6);

            var count = TenGoText.Create(card, $"{completed} / {total}", Fonts.AvenirBold, 18, ink, P(width / 2 - 24, 18), Z + 2,
                horizontal: HorizontalAlignmentOptions.Right);
            var name = TenGoText.Create(card, Loc.T(PuzzleCatalog.NameKey(_world)), Fonts.AvenirBold, 21, ink, P(-width / 2 + 24, 18), Z + 2,
                horizontal: HorizontalAlignmentOptions.Left);
            name.ShrinkToFit(width - 48 - count.WidthPt() - 14);

            float barW = width - 48, barH = 12;
            ShapeView.Rect("Track", card, P(0, -20), new Vector2(barW, barH), barH / 2, Colors.White(1, 0.55f), default, 0, Z + 1);
            float ratio = total > 0 ? completed / (float)total : 0;
            if (ratio > 0)
            {
                float fillW = Mathf.Max(barH, barW * Mathf.Min(1, ratio));
                ShapeView.Rect("Fill", card, P(-barW / 2 + fillW / 2, -20), new Vector2(fillW, barH), barH / 2,
                    tint.LedgeShade().LedgeShade(), default, 0, Z + 2);
            }
        }

        /// Action principale : enchaîner sur le prochain niveau sans le chercher.
        void AddPlayButton(PuzzleLevel level, float y, float width, float height)
        {
            var tint = Look.BubbleColor(4);
            var ink = tint.ReadableInk();
            var button = RenderShared.Node("playNext", Root, P(0, y));
            var bg = ShapeView.Rect("Bg", button, Vector2.zero, new Vector2(width, height), height / 2, tint, default, 0, Z);
            Relief.Raise(bg, 6);
            var label = TenGoText.Create(button, Loc.F("puzzle.level_label", level.Index), Fonts.AvenirBold, 24, ink, Vector2.zero, Z + 2,
                horizontal: HorizontalAlignmentOptions.Left);
            const float iconSize = 30, inner = 12;
            float contentW = iconSize + inner + label.WidthPt();
            VectorIcon.Create(button, VectorIconName.IconPlay, P(-contentW / 2 + iconSize / 2, 0), iconSize, ink, Z + 2);
            label.transform.localPosition = P(-contentW / 2 + iconSize + inner, 0);
            int w = level.World, i = level.Index;
            AddTarget("playNext", button, new Vector2(width, height), () => Play(w, i));
        }

        void AddLevelTile(PuzzleLevel level, Vector2 pos, float side, bool isNext, int colorIndex)
        {
            var progress = S.Puzzles;
            bool unlocked = progress.IsUnlocked(level.World, level.Index);
            int stars = progress.Stars(level.World, level.Index);
            float radius = side / 2;
            var tile = RenderShared.Node("level_" + level.Index, Root, P(pos.x, pos.y));

            // Niveau à jouer : un anneau accent le désigne dans la grille.
            if (isNext) ShapeView.Circle("Ring", tile, Vector2.zero, radius + 7, Look.BubbleColor(4), default, 0, Z - 1);

            var tint = Look.BubbleColor(new[] { 7, 3, 4, 6, 2, 9, 1, 5, 8 }[colorIndex % 9]);
            Color fill = !unlocked ? Look.Logo.WithAlpha(0.08f) : stars > 0 ? tint : Relief.Surface;
            var bg = ShapeView.Circle("Bg", tile, Vector2.zero, radius, fill, default, 0, Z);
            // Seuls les niveaux jouables prennent du relief.
            if (unlocked && !isNext) Relief.Raise(bg, 4);

            if (unlocked)
            {
                var ink = stars > 0 ? tint.ReadableInk() : Look.Logo;
                TenGoText.Create(tile, level.Index.ToString(), Fonts.AvenirHeavy, side * 0.34f, ink, P(0, side * 0.09f), Z + 2);
                // ★★☆ : trois étoiles dessinées (absentes de Nunito).
                float fs = side * 0.15f;
                float step = fs * 0.92f;
                var starColor = ink.WithAlpha(stars > 0 ? 0.9f : 0.5f);
                for (int k = 0; k < 3; k++)
                    Glyphs.Star(tile, P((k - 1) * step, -side * 0.21f), fs, starColor, k < stars, Z + 2);
                int w = level.World, i = level.Index;
                AddTarget("level_" + level.Index, tile, new Vector2(side, side), () => Play(w, i));
            }
            else
            {
                // `lock.fill` : cadenas plein (corps arrondi + anse).
                Lock(tile, side * 0.26f, Look.Logo, Z + 2);
            }
        }

        /// Cadenas plein (SF Symbol `lock.fill`), `h` = hauteur totale (points).
        static void Lock(Transform parent, float h, Color color, int order)
        {
            var node = RenderShared.Node("Lock", parent);
            float bodyW = h * 0.82f, bodyH = h * 0.58f;
            float bodyY = -h / 2 + bodyH / 2;
            ShapeView.Rect("Body", node, P(0, bodyY), new Vector2(bodyW, bodyH), h * 0.1f, color, default, 0, order);
            float r = bodyW * 0.30f, w = h * 0.13f;
            float baseY = bodyY + bodyH / 2;
            var pts = new System.Collections.Generic.List<Vector2>();
            pts.Add(P(-r, baseY));
            for (int i = 0; i <= 12; i++)
            {
                float a = Mathf.PI - Mathf.PI * i / 12f;
                pts.Add(P(Mathf.Cos(a) * r, baseY + h * 0.14f + Mathf.Sin(a) * r));
            }
            pts.Add(P(r, baseY));
            StrokeView.Create("Shackle", node, pts, color, w, order);
        }

        void AddBackButton(Vector2 pos)
        {
            var back = RenderShared.Node("back", Root, P(pos.x, pos.y));
            var circle = ShapeView.Circle("Bg", back, Vector2.zero, 30, Relief.Surface, default, 0, Z);
            Relief.Raise(circle, 4);
            // « ‹ » d'Avenir Next Medium 34 pt, en trait.
            StrokeView.Create("Chevron", back, new[] { P(2.5f, 7.5f), P(-4f, 0f), P(2.5f, -7.5f) }, Look.Logo, 2.6f, Z + 2);
            AddTarget("back", back, new Vector2(60, 60), () => App.Router.ShowMenu());
        }

        void Play(int world, int index) => App.Router.StartGame(GameLaunch.Puzzle(world, index));

        /// Sélection d'un niveau (tests) ; faux s'il est verrouillé.
        public bool SelectLevel(int index)
        {
            if (!S.Puzzles.IsUnlocked(_world, index) || S.Catalog.Level(_world, index) == null) return false;
            Play(_world, index);
            return true;
        }
    }
}
