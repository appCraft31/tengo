using System.Collections.Generic;
using TenGo.Fx;
using UnityEngine;

namespace TenGo.Rendering
{
    /// Ligne de tracé selon le style cosmétique (TrailRenderer.swift), pour le
    /// jeu et les aperçus de la boutique.
    public static class TrailRenderer
    {
        /// `tension` ∈ [0,1] = progression vers 10. Rampe volontairement NON
        /// linéaire (t^2,2) : l'écart ne devient lisible que dans le dernier
        /// quart, pour que le trait ne fasse pas le calcul à la place du joueur.
        /// `points` en unités locales de `parent` (centres des bulles).
        public static Transform Make(Transform parent, IList<Vector2> points, TrailKind style, Color accent,
            float tension = 0f, int order = Layers.Trail)
        {
            var container = RenderShared.Node("Trail", parent);
            float t = Mathf.Pow(Mathf.Clamp01(tension), 2.2f);
            float extra = 2.5f * t;
            float lift = 0.35f * t;

            void Stroke(Color color, float width, bool dashed = false)
            {
                var s = StrokeView.Create("Stroke", container, points, color.MixedWithWhite(lift), width + extra, order);
                if (dashed) s.SetDash(0.1f * width, 1.9f * width);
            }

            // Refonte 4.0 : tous les tracés sont des aplats à bouts ronds, sans comète.
            float u = Ui.Design.Pt(1);
            switch (style)
            {
                // Trait encre à 55 %, 7 pt.
                case TrailKind.Classic: Stroke(Ui.Design.Ink.WithAlpha(0.55f), 7 * u); break;
                // Néon : âme d'accent et liseré plat, sans flou.
                case TrailKind.Neon:
                    Stroke(accent.WithAlpha(0.28f), 14 * u);
                    Stroke(accent, 5 * u);
                    break;
                // Ruban : large bande d'accent.
                case TrailKind.Ribbon: Stroke(accent.WithAlpha(0.6f), 12 * u); break;
                // Pointillé : points ronds d'encre.
                case TrailKind.Dotted: Stroke(Ui.Design.Ink.WithAlpha(0.7f), 7 * u, dashed: true); break;
                // Encre : trait plein, plus fin et plus sombre que le classique.
                case TrailKind.Ink: Stroke(Ui.Design.Ink.WithAlpha(0.9f), 4.5f * u); break;
            }
            return container;
        }

        /// Comète en tête de tracé (GameScene.moveComet) : retirée par la refonte
        /// 4.0, les tracés en aplat se lisent seuls. Rend la comète existante
        /// effacée (`null` si aucune), pour les appelants qui la gardent.
        public static ShapeView MoveComet(ShapeView comet, Transform parent, Vector2 position, float tension, Color accent,
            int order = Layers.Comet)
        {
            if (comet != null) { RenderShared.Destroy(comet.gameObject); }
            return null;
        }
    }
}
