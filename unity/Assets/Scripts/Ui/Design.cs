using TenGo.App;
using TenGo.Fx;
using TenGo.Rendering;
using TMPro;
using UnityEngine;

namespace TenGo.Ui
{
    /// Système visuel de la refonte 4.0 (direction A « plat crème »), repris de
    /// `design/refonte/systeme.css` et `systeme.js` :
    /// - aplats, aucune ombre portée ni rebord 3D ; le seul relief est un filet
    ///   de 1 pt sous les surfaces blanches (`--hair`) ;
    /// - une seule couleur d'action (`Accent`), la couleur des cartes porte un sens ou rien ;
    /// - quatre tailles de texte (`Display`, `Title`, `Body`, `Small`).
    ///
    /// Les maquettes sont en points d'interface (téléphone de 390 × 844 pt).
    /// La scène travaille en unités de 750 × 1334 aspectFill : `Pt(x)` convertit
    /// avec le facteur réel de l'appareil (`SceneGeometry`), borné pour qu'une
    /// tablette ou un téléphone 16:9 garde une mise en page lisible.
    ///
    /// Thème par défaut : les valeurs exactes de la maquette. Autres thèmes (revus
    /// avec la Boutique) : les mêmes rôles dérivés du fond et de l'encre du thème.
    public static class Design
    {
        // MARK: - Couleurs de la maquette

        public static readonly Color BgHex = Hex(0xF7F3EC);
        public static readonly Color CardHex = Hex(0xFFFFFF);
        public static readonly Color SunkHex = Hex(0xEFE9DF);
        public static readonly Color LineHex = Hex(0xECE5D9);
        public static readonly Color HairHex = Hex(0xE6DED1);
        public static readonly Color InkHex = Hex(0x34312C);
        public static readonly Color Ink2Hex = Hex(0x6F675B);
        public static readonly Color Ink3Hex = Hex(0x8C8376);
        public static readonly Color AccentHex = Hex(0x65BD90);
        public static readonly Color AccentInk = Hex(0x1F3D2E);
        public static readonly Color Gold = Hex(0xE8B84A);
        public static readonly Color GoldRim = Hex(0xC9962A);
        public static readonly Color Danger = Hex(0xD9534F);
        /// Flamme de la série (maquette de l'accueil).
        public static readonly Color Flame = Hex(0xD9733A);
        /// Pousse de la graine (maquette de l'accueil).
        public static readonly Color SeedGreen = Hex(0x3E8A5F);
        /// Pierre du défi du jour.
        public static readonly Color Stone = Hex(0xD9D3C9);

        /// Palette des bulles 1 → 9 (`PAL` de systeme.js) : distinctes en
        /// deutéranopie, protanopie et tritanopie, chiffre encre ≥ 4,5:1.
        public static readonly int[] BubblePaletteHex =
            { 0xFACFCC, 0xDB9661, 0xD7C06C, 0xC6E99B, 0x65BD90, 0x7FEFFB, 0x97C6F7, 0xA99BE6, 0xF0A5D7 };

        // MARK: - Rôles selon le thème actif

        static bool IsDark => Look.Background.PerceivedLuminance() < 0.5f;
        static bool IsDefault => Look.Theme.IsDefault;

        public static Color Bg => Look.Background;
        /// Surfaces (cartes, boutons secondaires).
        public static Color Card => IsDefault ? CardHex : IsDark ? Look.Background.MixedWithWhite(0.12f) : Color.white;
        /// Champs, pistes, états inactifs.
        public static Color Sunk => IsDefault ? SunkHex : IsDark ? Look.Background.MixedWithWhite(0.07f) : Look.Logo.WithAlpha(0.07f).Flattened(Look.Background);
        /// Séparateurs, pistes de progression.
        public static Color Line => IsDefault ? LineHex : Look.Logo.WithAlpha(IsDark ? 0.16f : 0.10f).Flattened(Look.Background);
        /// Le filet sous les surfaces.
        public static Color Hair => IsDefault ? HairHex : Look.Logo.WithAlpha(IsDark ? 0.10f : 0.13f).Flattened(Look.Background);
        public static Color Ink => IsDefault ? InkHex : Look.Logo;
        public static Color Ink2 => IsDefault ? Ink2Hex : Look.Logo.WithAlpha(0.78f);
        public static Color Ink3 => IsDefault ? Ink3Hex : Look.Logo.WithAlpha(0.62f);
        /// Action principale (= bulle 5 du thème par défaut).
        public static Color Accent => IsDefault ? AccentHex : Look.Accent;
        /// Thèmes payants : l'accent est une de leurs bulles, son encre est donc le chiffre (≥ 4,5:1).
        public static Color OnAccent => IsDefault ? AccentInk : Look.Digit;

        /// Teinte d'une pastille d'icône (`tint(col, .45)` sur blanc).
        public static Color Tint(Color c, float alpha = 0.45f) => c.WithAlpha(alpha).Flattened(Card);

        // MARK: - Échelle

        /// Unités de scène par point de maquette (iPhone 17 Pro Max : 1,395).
        public static float Scale { get; private set; } = ScaleFor(SceneGeometry.IPhone17ProMax);
        /// Largeur et hauteur visibles en points de maquette.
        public static float WidthPt { get; private set; } = SceneGeometry.IPhone17ProMax.VisibleWidth / Scale;
        public static float HeightPt { get; private set; } = SceneGeometry.IPhone17ProMax.VisibleHeight / Scale;

        /// Facteur réel (pixels par point / pixels par unité de scène), borné :
        /// la largeur visible reste entre 360 et 480 pt, la hauteur au moins 760 pt.
        public static float ScaleFor(SceneGeometry g)
        {
            float real = g.PixelsPerPoint / g.PixelsPerScene;
            float s = Mathf.Clamp(real, g.VisibleWidth / 480f, g.VisibleWidth / 360f);
            return Mathf.Min(s, g.VisibleHeight / 760f);
        }

        /// Appelé par `AppRoot` à chaque changement de géométrie.
        public static void Apply(SceneGeometry g)
        {
            Scale = ScaleFor(g);
            WidthPt = g.VisibleWidth / Scale;
            HeightPt = g.VisibleHeight / Scale;
        }

        /// Points de maquette → unités de scène.
        public static float Pt(float pt) => pt * Scale;
        public static Vector2 Pt(float x, float y) => new(x * Scale, y * Scale);

        // MARK: - Mesures (systeme.css)

        public const float CardRadius = 20, ButtonRadius = 18;
        public const float Gutter = 16;
        public const float HairPt = 1;
        public const float IconButtonPt = 44, IconPt = 22;

        /// Échelle typographique : 4 tailles.
        public const float DisplayPt = 40, TitlePt = 26, BodyPt = 16, SmallPt = 13;

        // MARK: - Briques

        /// Surface blanche (ou du thème) avec son filet de 1 pt dessous. `size` en unités de scène.
        public static ShapeView Surface(string name, Transform parent, Vector2 scenePos, Vector2 size, float cornerScene,
            int order, Color? fill = null, bool hair = true)
        {
            if (hair)
                ShapeView.Rect(name + "Hair", parent, Units.P(scenePos.x, scenePos.y - Pt(HairPt)), size, cornerScene, Hair, default, 0, order - 1);
            return ShapeView.Rect(name, parent, Units.P(scenePos.x, scenePos.y), size, cornerScene, fill ?? Card, default, 0, order);
        }

        /// Disque blanc avec filet (boutons ronds, pastilles).
        public static ShapeView Disc(string name, Transform parent, Vector2 scenePos, float radiusScene, int order, Color? fill = null, bool hair = true)
        {
            if (hair)
                ShapeView.Circle(name + "Hair", parent, Units.P(scenePos.x, scenePos.y - Pt(HairPt)), radiusScene, Hair, default, 0, order - 1);
            return ShapeView.Circle(name, parent, Units.P(scenePos.x, scenePos.y), radiusScene, fill ?? Card, default, 0, order);
        }

        /// Bouton rond blanc 44 pt et son icône au trait 22 pt (`.iconbtn`).
        public static Transform IconButton(string name, Transform parent, Vector2 scenePos, VectorIconName icon, int order,
            float sizePt = IconButtonPt, float iconPt = IconPt)
        {
            var node = RenderShared.Node(name, parent, Units.P(scenePos.x, scenePos.y));
            Disc("Bg", node, Vector2.zero, Pt(sizePt / 2), order);
            VectorIcon.Create(node, icon, Vector2.zero, Pt(iconPt), Ink, order + 2);
            return node;
        }

        /// Texte en points de maquette.
        public static TenGoText Text(Transform parent, string text, NunitoWeight weight, float sizePt, Color color,
            Vector2 scenePos, int order, HorizontalAlignmentOptions align = HorizontalAlignmentOptions.Center) =>
            TenGoText.Create(parent, text, weight, Pt(sizePt), color, Units.P(scenePos.x, scenePos.y), order, horizontal: align);

        /// Pièce : disque or cerclé d'un filet plus foncé, sans reflet.
        public static Transform Coin(Transform parent, Vector2 scenePos, float diameterPt, int order)
        {
            var coin = RenderShared.Node("Coin", parent, Units.P(scenePos.x, scenePos.y));
            float r = Pt(diameterPt / 2);
            ShapeView.Circle("Disc", coin, Vector2.zero, r - Pt(1), Gold, GoldRim, Pt(2), order);
            return coin;
        }

        /// Pastille capsule (`height` en points de maquette), couleur `fill`.
        public static ShapeView Pill(string name, Transform parent, Vector2 scenePos, float widthScene, float heightPt, Color fill,
            int order, bool hair = false) =>
            Surface(name, parent, scenePos, new Vector2(widthScene, Pt(heightPt)), Pt(heightPt / 2), order, fill, hair);

        /// Pastille carrée arrondie teintée portant une icône au trait (`.ic` des listes).
        public static Transform IconChip(Transform parent, Vector2 scenePos, float sizePt, float radiusPt, Color tint,
            VectorIconName icon, int order, Color? iconColor = null, float iconPt = 0, float alpha = 0.45f)
        {
            var node = RenderShared.Node("Chip", parent, Units.P(scenePos.x, scenePos.y));
            ShapeView.Rect("Bg", node, Vector2.zero, Pt(sizePt, sizePt), Pt(radiusPt), Tint(tint, alpha), default, 0, order);
            VectorIcon.Create(node, icon, Vector2.zero, Pt(iconPt > 0 ? iconPt : sizePt * 0.56f), iconColor ?? Ink, order + 1);
            return node;
        }

        /// Barre de progression plate (piste `Line`), ancrée par son bord gauche ; tailles en points de maquette.
        public static void Bar(Transform parent, Vector2 leftScenePos, float widthPt, float heightPt, float ratio, Color fill, int order,
            Color? track = null)
        {
            float w = Pt(widthPt), h = Pt(heightPt);
            ShapeView.Rect("Track", parent, Units.P(leftScenePos.x + w / 2, leftScenePos.y), new Vector2(w, h), h / 2, track ?? Line, default, 0, order);
            if (ratio <= 0) return;
            float fw = Mathf.Max(h, w * Mathf.Clamp01(ratio));
            ShapeView.Rect("Fill", parent, Units.P(leftScenePos.x + fw / 2, leftScenePos.y), new Vector2(fw, h), h / 2, fill, default, 0, order + 1);
        }

        /// Mini bulle numérotée (`.mini i` : 18 pt, chiffre 10 pt Black) ; `value` 0 = « ? » sur piste.
        public static void MiniBubble(Transform parent, Vector2 scenePos, float diameterPt, int value, int order)
        {
            var fill = value > 0 ? Look.BubbleColor(value) : Sunk;
            ShapeView.Circle("Mini", parent, Units.P(scenePos.x, scenePos.y), Pt(diameterPt / 2), fill, default, 0, order);
            Text(parent, value > 0 ? value.ToString() : "?", NunitoWeight.Black, diameterPt * 0.56f, value > 0 ? Look.Digit : Ink3,
                scenePos, order + 1);
        }

        /// Bouton plein largeur : principal (accent) ou secondaire (carte + filet). Icône facultative.
        /// `heightPt` 56 par défaut ; renvoie le nœud (centré sur `scenePos`).
        public static Transform Button(string name, Transform parent, Vector2 scenePos, float widthScene, string label, bool primary,
            int order, VectorIconName? icon = null, float heightPt = 56, string subtitle = null, float labelPt = 0)
        {
            var node = RenderShared.Node(name, parent, Units.P(scenePos.x, scenePos.y));
            FxAlpha.Of(node.gameObject);
            Surface("Bg", node, Vector2.zero, new Vector2(widthScene, Pt(heightPt)), Pt(ButtonRadius), order, primary ? Accent : Card, hair: !primary);
            var ink = primary ? OnAccent : Ink;
            float size = labelPt > 0 ? labelPt : primary ? 18 : 17;
            float lineY = subtitle != null ? Pt(9) : 0;
            var t = Text(node, label, primary ? NunitoWeight.Black : NunitoWeight.ExtraBold, size, ink, new Vector2(0, lineY), order + 3,
                HorizontalAlignmentOptions.Left);
            float iconW = icon != null ? Pt(22) : 0, gap = icon != null ? Pt(8) : 0;
            float maxLabel = widthScene - Pt(32) - iconW - gap;
            if (t.WidthPt() > maxLabel) t.ShrinkToFit(maxLabel);
            float labelW = Mathf.Min(t.WidthPt(), maxLabel);
            float total = iconW + gap + labelW;
            if (icon is VectorIconName ic)
                VectorIcon.Create(node, ic, Units.P(-total / 2 + iconW / 2, lineY), iconW, ink, order + 2);
            t.transform.localPosition = Units.P(-total / 2 + iconW + gap, lineY);
            if (subtitle != null)
                Text(node, subtitle, NunitoWeight.Bold, 12, ink.WithAlpha(0.8f), new Vector2(0, -Pt(12)), order + 3).ShrinkToFit(widthScene - Pt(32));
            return node;
        }

        /// En-tête des sous-écrans : bouton retour rond à gauche, titre 30 pt, texte annexe à droite.
        /// `rowY` = centre de la rangée (unités de scène) ; renvoie le bouton retour.
        public static Transform BackHeader(Transform parent, float leftX, float rightX, float rowY, string title, string trailing, int order,
            out TenGoText titleLabel)
        {
            var back = IconButton("back", parent, new Vector2(leftX + Pt(IconButtonPt / 2), rowY), VectorIconName.LineBack, order);
            TenGoText trail = null;
            if (!string.IsNullOrEmpty(trailing))
                trail = Text(parent, trailing, NunitoWeight.ExtraBold, 14, Ink2, new Vector2(rightX, rowY), order + 2, HorizontalAlignmentOptions.Right);
            float titleX = leftX + Pt(IconButtonPt + 12);
            titleLabel = Text(parent, title, NunitoWeight.Black, 30, Ink, new Vector2(titleX, rowY), order + 2, HorizontalAlignmentOptions.Left);
            float maxW = rightX - titleX - (trail != null ? trail.WidthPt() + Pt(10) : 0);
            if (titleLabel.WidthPt() > maxW) titleLabel.ShrinkToFit(maxW);
            return back;
        }

        /// Contrôle segmenté (`.seg`) : piste creusée, segment actif en carte. Renvoie les nœuds des segments.
        public static Transform[] Segmented(Transform parent, Vector2 scenePos, float widthScene, string[] labels, int selected, int order,
            float heightPt = 40)
        {
            var node = RenderShared.Node("Segmented", parent, Units.P(scenePos.x, scenePos.y));
            float h = Pt(heightPt), pad = Pt(4);
            ShapeView.Rect("Track", node, Vector2.zero, new Vector2(widthScene, h), Pt(14), Sunk, default, 0, order);
            float segW = (widthScene - 2 * pad) / labels.Length;
            var nodes = new Transform[labels.Length];
            for (int i = 0; i < labels.Length; i++)
            {
                float x = -widthScene / 2 + pad + segW * (i + 0.5f);
                var seg = RenderShared.Node("Seg" + i, node, Units.P(x, 0));
                if (i == selected) Surface("Bg", seg, Vector2.zero, new Vector2(segW, h - 2 * pad), Pt(11), order + 1);
                Text(seg, labels[i], NunitoWeight.ExtraBold, 13, i == selected ? Ink : Ink2, Vector2.zero, order + 3).ShrinkToFit(segW - Pt(8));
                nodes[i] = seg;
            }
            return nodes;
        }

        /// Rond blanc du niveau cerclé de l'anneau d'XP (bulle 8, depuis le haut, sens horaire).
        public static void LevelRing(Transform parent, Vector2 scenePos, Core.LevelBreakdown level, float discPt, float ringPt, float ringWidthPt, int order)
        {
            Disc("Disc", parent, scenePos, Pt(discPt), order);
            float ringR = Pt(ringPt), ringW = Pt(ringWidthPt);
            ShapeView.Circle("Track", parent, Units.P(scenePos.x, scenePos.y), ringR, Color.clear, Line, ringW, order + 1);
            int into = level.XpIntoLevel, forNext = level.XpForNext;
            float ratio = forNext > 0 ? Mathf.Clamp01(into / (float)forNext) : 1f;
            if (ratio > 0.005f)
            {
                var arc = new System.Collections.Generic.List<Vector2>();
                int n = Mathf.Max(2, Mathf.CeilToInt(ratio * (StrokeView.MaxPoints - 1)));
                for (int i = 0; i <= n; i++)
                {
                    float a = Mathf.PI / 2f - ratio * Mathf.PI * 2f * i / n;
                    arc.Add(Units.P(scenePos.x + Mathf.Cos(a) * ringR, scenePos.y + Mathf.Sin(a) * ringR));
                }
                StrokeView.Create("Arc", parent, arc, Look.BubbleColor(8), ringW, order + 2);
            }
        }

        /// Chevron « › » des lignes de liste.
        public static void Chevron(Transform parent, Vector2 scenePos, int order, float sizePt = 18, Color? color = null) =>
            VectorIcon.Create(parent, VectorIconName.LineChevron, Units.P(scenePos.x, scenePos.y), Pt(sizePt), color ?? Ink3, order);

        /// Nombre groupé par milliers (« 4 820 »), espace simple pour les langues à espace insécable.
        public static string Number(int v)
        {
            string sep;
            try { sep = new System.Globalization.CultureInfo(Core.Loc.Language ?? "fr").NumberFormat.NumberGroupSeparator; }
            catch (System.Globalization.CultureNotFoundException) { sep = " "; }
            if (sep == " " || sep == " " || string.IsNullOrEmpty(sep)) sep = " ";
            var nfi = new System.Globalization.NumberFormatInfo { NumberGroupSeparator = sep, NumberGroupSizes = new[] { 3 } };
            return v.ToString("#,0", nfi);
        }

        public static Color Hex(int rgb, float a = 1f) =>
            new(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, a);
    }
}
