using UnityEngine;

namespace TenGo.Rendering
{
    /// Finition commune des boutons, cartes et pastilles. Refonte 4.0
    /// (direction A « plat crème ») : plus de socle, d'ombre ni de reflet ;
    /// une surface neutre reçoit seulement un filet de 1 pt dessous
    /// (`--hair` de systeme.css), une surface colorée reste un aplat. Les
    /// écrans non encore refaits passent tous par ici et s'aplatissent ainsi.
    public static class Relief
    {
        /// Surface neutre des éléments secondaires : blanc sur fond clair (`Design.Card`).
        public static Color Surface => Ui.Design.Card;

        /// Filet (et anciens socle, ombre, reflet, toujours nuls).
        public sealed class Parts
        {
            public ShapeView Ledge, Halo;
            public StrokeView Highlight;
        }

        /// Aplatit `shape` et pose son filet. `depthPt` n'est plus utilisé (relief
        /// supprimé) ; `surface` : couleur `Relief.Surface` ; `fill` : remplace la couleur.
        public static Parts Raise(ShapeView shape, float depthPt = 6f, bool surface = false, Color? fill = null)
        {
            if (fill is { } f) shape.Fill = f;
            if (surface) shape.Fill = Surface;
            shape.Fill = shape.Fill.Flattened(Look.Background);
            shape.Stroke = Color.clear;
            shape.LineWidthPt = 0;

            var parent = shape.transform.parent;
            var pos = (Vector2)shape.transform.localPosition;
            Color.RGBToHSV(shape.Fill, out _, out var sat, out _);
            // Filet seulement sous une surface neutre (blanc, crème) ; un aplat coloré se suffit.
            var hair = sat < 0.12f ? Ui.Design.Hair : Color.clear;
            var parts = new Parts
            {
                Ledge = Clone(shape, "Ledge", parent, pos + Units.P(0, -Ui.Design.Pt(Ui.Design.HairPt)), hair, shape.SortingOrder - 1),
            };
            parts.Ledge.transform.SetSiblingIndex(shape.transform.GetSiblingIndex());
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
