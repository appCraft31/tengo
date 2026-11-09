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
                if (dashed) s.SetDash(2, 14);
            }

            switch (style)
            {
                // Refonte 4.0 : trait encre à 55 %, 7 pt, bouts ronds.
                case TrailKind.Classic: Stroke(Ui.Design.Ink.WithAlpha(0.55f), Ui.Design.Pt(7)); break;
                case TrailKind.Neon:
                    Stroke(accent.WithAlpha(0.25f), 16);   // halo
                    Stroke(accent, 5);
                    break;
                case TrailKind.Ribbon: Stroke(accent.WithAlpha(0.45f), 14); break;
                case TrailKind.Dotted: Stroke(Colors.White(1, 0.9f), 7, dashed: true); break;
                case TrailKind.Ink: Stroke(Colors.White(0.18f, 0.7f), 7); break;
            }
            return container;
        }

        /// Comète en tête de tracé (GameScene.moveComet) : disque doux additif,
        /// teinte d'accent blanchie avec la tension, taille 26 + 16·t, alpha
        /// 0,45 + 0,35·t, glisse en 0,06 s. `null` sans animations.
        public static ShapeView MoveComet(ShapeView comet, Transform parent, Vector2 position, float tension, Color accent,
            int order = Layers.Comet)
        {
            if (!JuiceSettings.MotionEnabled) return comet;
            // Refonte 4.0 : le trait encre classique se lit seul, sans halo en tête.
            if (Look.Trail == TrailKind.Classic) return comet;
            if (comet == null)
            {
                comet = ShapeView.Dot("Comet", parent, position, 26, Color.white, additive: true, order);
            }
            comet.Fill = accent.MixedWithWhite(0.25f + 0.45f * tension);
            float size = 26 + 16 * tension;
            comet.SizePt = new Vector2(size, size);
            FxAlpha.Of(comet.gameObject).Alpha = 0.45f + 0.35f * tension;
            Tw.Run(comet.gameObject, Tw.MoveTo(comet.transform, position, 0.06f), "move");
            return comet;
        }
    }
}
