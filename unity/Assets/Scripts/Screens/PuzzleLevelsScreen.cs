using System.Linq;
using TenGo.App;
using TenGo.Core;
using TenGo.Rendering;
using TenGo.Ui;
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

        static float Pt(float v) => Design.Pt(v);

        /// Refonte 4.0 (`design/refonte/06_autres`) : retour en haut à gauche et
        /// étoiles d'or à droite, carte du monde blanche, niveaux en tuiles
        /// carrées blanches (le prochain cerclé d'encre, verrouillés en creux),
        /// bouton principal « Niveau N » en bas.
        void Build()
        {
            float contentW = ContentW;
            var levels = S.Catalog.Levels(_world);
            var progress = S.Puzzles;
            int completed = progress.CompletedCount(_world);
            int stars = progress.TotalStars(_world);

            // En-tête : retour, titre, étoiles (le texte annexe laisse la place à l'étoile).
            string starsText = $"{stars} / {levels.Count * 3}";
            AddBackHeader(Loc.T("menu.puzzles"), starsText, () => App.Router.ShowMenu());
            var probe = Design.Text(Root, starsText, NunitoWeight.ExtraBold, 14, Color.clear, Vector2.zero, 0);
            Glyphs.Star(Root, Units.P(contentW / 2 - probe.WidthPt() - Pt(11), HeaderRowY + Pt(1)), Pt(15), Design.GoldRim, true, Z + 2);
            RenderShared.Destroy(probe.gameObject);

            float cursor = HeaderBottom - Pt(10);
            float cardH = Pt(76);
            AddWorldCard(cursor - cardH / 2, contentW, cardH, completed, levels.Count);
            cursor -= cardH + Pt(14);

            // Niveau à jouer : le premier déverrouillé pas encore résolu.
            var next = levels.FirstOrDefault(l => progress.IsUnlocked(l.World, l.Index) && progress.Stars(l.World, l.Index) == 0);
            NextLevelIndex = next?.Index ?? 0;
            float ctaH = Pt(56);
            float bottom = -HalfH + BottomInset + Pt(12);
            float ctaY = bottom + ctaH / 2;
            if (next != null) AddPlayButton(next, ctaY, contentW, ctaH);

            // Grille de tuiles : 4 colonnes, tassées si l'écran est court.
            const int perRow = 4;
            int rows = Mathf.CeilToInt(levels.Count / (float)perRow);
            float gap = Pt(10);
            float gridBottom = (next == null ? bottom : ctaY + ctaH / 2) + Pt(16);
            float byWidth = (contentW - gap * (perRow - 1)) / perRow;
            float byHeight = (cursor - gridBottom - gap * Mathf.Max(0, rows - 1)) / Mathf.Max(1, rows);
            float cell = Mathf.Min(byWidth, byHeight);
            float gridW = cell * perRow + gap * (perRow - 1);
            for (int offset = 0; offset < levels.Count; offset++)
            {
                var level = levels[offset];
                int column = offset % perRow, row = offset / perRow;
                float x = -gridW / 2 + cell / 2 + column * (cell + gap);
                float y = cursor - cell / 2 - row * (cell + gap);
                AddLevelTile(level, new Vector2(x, y), cell, next != null && level.Index == next.Index);
            }
        }

        void AddWorldCard(float y, float width, float height, int completed, int total)
        {
            var card = RenderShared.Node("WorldCard", Root, Units.P(0, y));
            Design.Surface("Bg", card, Vector2.zero, new Vector2(width, height), Pt(Design.CardRadius), Z);
            float lx = -width / 2 + Pt(16), rx = width / 2 - Pt(16);
            var count = Design.Text(card, $"{completed} / {total}", NunitoWeight.ExtraBold, 13, Design.Ink2, new Vector2(rx, Pt(12)), Z + 2,
                HorizontalAlignmentOptions.Right);
            Design.Text(card, Loc.T(PuzzleCatalog.NameKey(_world)), NunitoWeight.Black, 16, Design.Ink, new Vector2(lx, Pt(12)), Z + 2,
                HorizontalAlignmentOptions.Left).ShrinkToFit(rx - lx - count.WidthPt() - Pt(12));
            float ratio = total > 0 ? completed / (float)total : 0;
            Design.Bar(card, new Vector2(lx, -Pt(14)), (rx - lx) / Design.Scale, 8, ratio, Design.Ink, Z + 2);
        }

        /// Action principale : enchaîner sur le prochain niveau sans le chercher.
        void AddPlayButton(PuzzleLevel level, float y, float width, float height)
        {
            var button = Design.Button("playNext", Root, new Vector2(0, y), width, Loc.F("puzzle.level_label", level.Index), true, Z,
                VectorIconName.LinePlay);
            int w = level.World, i = level.Index;
            AddTarget("playNext", button, new Vector2(width, height), () => Play(w, i));
        }

        void AddLevelTile(PuzzleLevel level, Vector2 pos, float side, bool isNext)
        {
            var progress = S.Puzzles;
            bool unlocked = progress.IsUnlocked(level.World, level.Index);
            int stars = progress.Stars(level.World, level.Index);
            var tile = RenderShared.Node("level_" + level.Index, Root, Units.P(pos.x, pos.y));
            float r = Pt(18);
            var size = new Vector2(side, side);

            if (!unlocked)
            {
                ShapeView.Rect("Bg", tile, Vector2.zero, size, r, Design.Sunk, default, 0, Z);
                VectorIcon.Create(tile, VectorIconName.LineLock, Vector2.zero, Pt(20), Design.Ink3, Z + 2);
                return;
            }
            // Niveau à jouer : cerclé d'encre.
            if (isNext) ShapeView.Rect("Ring", tile, Vector2.zero, size + Design.Pt(5, 5), r + Pt(2.5f), Design.Ink, default, 0, Z - 1);
            Design.Surface("Bg", tile, Vector2.zero, size, r, Z, hair: !isNext);
            Design.Text(tile, level.Index.ToString(), NunitoWeight.Black, 24, Design.Ink, new Vector2(0, side * 0.12f), Z + 2);
            // ★★☆ : trois étoiles dessinées (absentes de Nunito), or ou creux.
            float fs = Pt(13), step = Pt(15);
            for (int k = 0; k < 3; k++)
                Glyphs.Star(tile, Units.P((k - 1) * step, -side * 0.2f), fs, k < stars ? Design.GoldRim : Design.Hex(0xCFC6B8), k < stars, Z + 2);
            int w = level.World, i = level.Index;
            AddTarget("level_" + level.Index, tile, size, () => Play(w, i));
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
