using UnityEngine;

namespace TenGo.Rendering
{
    /// Finition commune des boutons, cartes et pastilles (Relief.swift) : un
    /// socle de la même teinte (plus soutenue) sous la forme, une ombre douce
    /// et un reflet en haut. Tout dérive de la couleur de la forme et du thème actif.
    public static class Relief
    {
        /// Surface neutre des éléments secondaires : le fond du thème tiré vers le blanc.
        public static Color Surface
        {
            get
            {
                var bg = Look.Background;
                return bg.MixedWithWhite(bg.PerceivedLuminance() > 0.5f ? 0.85f : 0.16f);
            }
        }

        /// Socle, ombre et reflet d'une forme déjà créée.
        public sealed class Parts
        {
            public ShapeView Ledge, Halo;
            public StrokeView Highlight;
        }

        /// Donne du relief à `shape` (rectangle arrondi ou cercle). Le socle et
        /// l'ombre sont des frères placés juste dessous (ordre − 1 et − 2).
        /// `depthPt` : hauteur du socle ; `surface` : couleur `Relief.Surface` ;
        /// `fill` : remplace la couleur.
        public static Parts Raise(ShapeView shape, float depthPt = 6f, bool surface = false, Color? fill = null)
        {
            if (fill is { } f) shape.Fill = f;
            if (surface) shape.Fill = Surface;
            // Un aplat translucide donnerait un socle visible au travers.
            shape.Fill = shape.Fill.Flattened(Look.Background);
            shape.Stroke = Color.clear;
            shape.LineWidthPt = 0;

            var baseColor = shape.Fill;
            var parent = shape.transform.parent;
            var pos = (Vector2)shape.transform.localPosition;
            var parts = new Parts();

            parts.Ledge = Clone(shape, "Ledge", parent, pos + Units.P(0, -depthPt), baseColor.LedgeShade(), shape.SortingOrder - 1);
            // Pas d'ombre floue sur un SKShapeNode : un second aplat plus bas, plus
            // étroit et très transparent en tient lieu.
            parts.Halo = Clone(shape, "Halo", parent, pos + Units.P(0, -depthPt - 6), baseColor.LedgeShade().WithAlpha(0.20f), shape.SortingOrder - 2);
            var hs = parts.Halo.transform.localScale;
            parts.Halo.transform.localScale = new Vector3(hs.x * 0.95f, hs.y, 1);
            parts.Ledge.transform.SetSiblingIndex(shape.transform.GetSiblingIndex());
            parts.Halo.transform.SetSiblingIndex(parts.Ledge.transform.GetSiblingIndex());

            // Reflet : un trait clair sous le bord haut. Sans objet sur un rond
            // ni sur une grande carte.
            var size = shape.SizePt;
            float inset = Mathf.Min(size.y / 2f, 26f);
            float length = size.x - inset * 2f;
            if (length > 12f && size.y < 150f)
            {
                float y = Units.FromPoints(size.y / 2f - 5f);
                float x0 = Units.FromPoints(-size.x / 2f + inset), x1 = Units.FromPoints(size.x / 2f - inset);
                parts.Highlight = StrokeView.Create("Highlight", shape.transform, new[] { new Vector2(x0, y), new Vector2(x1, y) },
                    Colors.White(1, baseColor.PerceivedLuminance() > 0.5f ? 0.50f : 0.16f), 3, shape.SortingOrder + 1);
            }
            return parts;
        }

        static ShapeView Clone(ShapeView s, string name, Transform parent, Vector2 pos, Color fill, int order)
        {
            var c = ShapeView.Create(name, parent, pos, s.Kind, s.SizePt, fill, default, 0, s.CornerPt, order);
            c.transform.localRotation = s.transform.localRotation;
            c.transform.localScale = s.transform.localScale;
            return c;
        }

        /// Rond clair derrière une icône, pour la détacher d'une tuile colorée.
        public static ShapeView IconChip(Transform parent, Vector2 pos, float radiusPt, Color on, int order) =>
            ShapeView.Circle("IconChip", parent, pos, radiusPt, Colors.White(1, on.PerceivedLuminance() > 0.5f ? 0.50f : 0.14f), default, 0, order);
    }
}
