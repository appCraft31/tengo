using TenGo.Rendering;
using UnityEngine;

namespace TenGo.Fx
{
    /// Célébration de la victoire (grille vidée, WinCelebration.swift) : trois
    /// ondes qui partent du centre de la grille, puis une pluie de confettis
    /// aux teintes de l'accent du thème. Se démonte seule après 3,2 s.
    public static class WinCelebration
    {
        public static Transform Make(Transform fxLayer, Vector2 center, Color accent, Vector2 sceneSizePt,
            System.Random rng = null, int order = Layers.Celebration)
        {
            rng ??= new System.Random();
            var container = RenderShared.Node("WinCelebration", fxLayer);
            for (int i = 0; i < 3; i++)
            {
                var ring = ShapeView.Ring("Sweep", container, center, 120, accent, order);
                ring.transform.localScale = Vector3.one * 0.4f;
                FxAlpha.Of(ring.gameObject).Alpha = 0f;
                Tw.Run(ring.gameObject, Tw.Seq(
                    Tw.Wait(i * 0.14f),
                    Tw.Fade(ring.gameObject, 0.55f, 0.08f),
                    Tw.Par(Tw.Scale(ring.transform, 9f, 0.75f).Ease(TwEase.EaseOut), Tw.FadeOut(ring.gameObject, 0.75f)),
                    Tw.Remove(ring.gameObject)));
            }
            Emitter.Create(container, Units.P(0, sceneSizePt.y / 2f + 40), EmitterSpec.Confetti(accent, sceneSizePt.x), order + 10, rng);
            Tw.Run(container.gameObject, Tw.Seq(Tw.Wait(3.2f), Tw.Remove(container.gameObject)));
            return container;
        }
    }
}
