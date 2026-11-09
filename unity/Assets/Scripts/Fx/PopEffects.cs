using TenGo.Rendering;
using UnityEngine;

namespace TenGo.Fx
{
    /// Effets de destruction des bulles (PopEffects.swift) : flash blanc, halo
    /// teinté, éclats radiaux et ondes annulaires selon le palier. Nœuds
    /// éphémères auto-détruits : leur durée ne retarde jamais la gravité.
    public static class PopEffects
    {
        /// Éclatement à `localPos` (unités) sous `parent` (la couche d'effets).
        public static Transform MakeBurst(Transform parent, Vector2 localPos, Color color, PopTier tier, System.Random rng = null,
            int order = Layers.Burst)
        {
            rng ??= new System.Random();
            var container = RenderShared.Node("Burst", parent, localPos);
            AddFlash(container, color, tier, order);
            int count = Mathf.Max(4, (int)(tier.BurstCount() * JuiceSettings.ParticleScale));
            SparkCloud.Create(container, color, count, tier.SparkReach(), order, rng);
            AddRings(container, color, tier, order);
            Tw.Run(container.gameObject, Tw.Seq(Tw.Wait(0.6f), Tw.Remove(container.gameObject)));
            return container;
        }

        static void AddFlash(Transform container, Color color, PopTier tier, int order)
        {
            float r = BubbleView.RadiusPt;
            var flash = ShapeView.Dot("Flash", container, Vector2.zero, r * 2.2f, Color.white, additive: true, order);
            FxAlpha.Of(flash.gameObject).Alpha = 0.55f;
            Tw.Run(flash.gameObject, Tw.Seq(
                Tw.Par(Tw.Scale(flash.transform, 1.6f, 0.15f), Tw.FadeOut(flash.gameObject, 0.15f)),
                Tw.Remove(flash.gameObject)));

            // Halo teinté : présent à tous les paliers, il donne du corps aux chaînes courtes.
            float span = tier == PopTier.Small ? 2.6f : 3.4f;
            var halo = ShapeView.Dot("Halo", container, Vector2.zero, r * span, color, additive: true, order);
            FxAlpha.Of(halo.gameObject).Alpha = tier == PopTier.Small ? 0.32f : 0.40f;
            float grow = tier switch { PopTier.Small => 1.4f, PopTier.Medium => 1.6f, _ => 2.1f };
            Tw.Run(halo.gameObject, Tw.Seq(
                Tw.Par(Tw.Scale(halo.transform, grow, 0.28f).Ease(TwEase.EaseOut), Tw.FadeOut(halo.gameObject, 0.28f)),
                Tw.Remove(halo.gameObject)));
        }

        static void AddRings(Transform container, Color color, PopTier tier, int order)
        {
            for (int i = 0; i < tier.RingCount(); i++)
            {
                var ring = ShapeView.Ring("Ring", container, Vector2.zero, BubbleView.RadiusPt * 2f, color.MixedWithWhite(0.2f), order);
                ring.transform.localScale = Vector3.one * 0.3f;
                Tw.Run(ring.gameObject, Tw.Seq(
                    Tw.Wait(i * 0.09f),
                    Tw.Par(Tw.Scale(ring.transform, i == 0 ? 2.2f : 3.0f, 0.35f).Ease(TwEase.EaseOut), Tw.FadeOut(ring.gameObject, 0.35f)),
                    Tw.Remove(ring.gameObject)));
            }
        }
    }
}
