using TMPro;
using UnityEngine;

namespace TenGo.Rendering
{
    /// Décale l'ordre d'affichage de tout un sous-arbre (`zPosition` relative
    /// de SpriteKit : coach-mark qui remonte sa cible au-dessus du voile).
    public static class OrderShift
    {
        public static void Shift(Transform root, int delta)
        {
            if (root == null || delta == 0) return;
            foreach (var s in root.GetComponentsInChildren<ShapeView>(true)) s.SortingOrder += delta;
            foreach (var s in root.GetComponentsInChildren<StrokeView>(true)) s.SortingOrder += delta;
            foreach (var s in root.GetComponentsInChildren<VectorIcon>(true)) s.SortingOrder += delta;
            foreach (var t in root.GetComponentsInChildren<TextMeshPro>(true)) t.sortingOrder += delta;
        }

        /// Cadre (en points de scène) des rendus (hors halos) de `root`, racine de scène à l'origine.
        public static Rect BoundsPt(Transform root)
        {
            bool any = false;
            var b = new Bounds();
            foreach (var r in root.GetComponentsInChildren<Renderer>(false))
            {
                // Ombres et halos éphémères débordent de la forme : le cadre suit la forme seule.
                if (r.gameObject.name == "Halo") continue;
                if (!any) { b = r.bounds; any = true; }
                else b.Encapsulate(r.bounds);
            }
            if (!any) return new Rect(Units.ToPoints(root.position.x), Units.ToPoints(root.position.y), 0, 0);
            return Rect.MinMaxRect(b.min.x * 100f, b.min.y * 100f, b.max.x * 100f, b.max.y * 100f);
        }
    }
}
