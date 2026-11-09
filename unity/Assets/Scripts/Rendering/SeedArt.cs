using TenGo.Fx;
using UnityEngine;

namespace TenGo.Rendering
{
    /// Dessins de la graine de dix (SeedArt.swift) : pousse, fleur, horloge,
    /// pastille « ×N », pulsation. Mêmes formes que l'app Android.
    public static class SeedArt
    {
        public static readonly Color GoldColor = new(0.98f, 0.76f, 0.22f);
        public static readonly Color Leaf = new(0.40f, 0.66f, 0.30f);
        public static readonly Color GaugeEmpty = new(0, 0, 0, 0.08f);

        /// Pousse : terre, tige, deux feuilles. `w` = côté du carré occupé (points).
        public static Transform Sprout(Transform parent, Vector2 pos, float w, int order, float alpha = 1f)
        {
            var node = RenderShared.Node("Sprout", parent, pos);
            ShapeView.Ellipse("Soil", node, Units.P(0, -w * 0.39f), new Vector2(w * 0.6f, w * 0.14f),
                new Color(0.55f, 0.40f, 0.25f, 0.35f * alpha), default, 0, order);
            StrokeView.Create("Stem", node, new[] { Units.P(0, -w * 0.38f), Units.P(0, w * 0.1f) }, Leaf.WithAlpha(alpha), w * 0.07f, order);
            foreach (var (dx, dy) in new[] { (-0.18f, 0.1f), (0.18f, 0.2f) })
                ShapeView.Ellipse("Leaf", node, Units.P(w * dx, w * dy), new Vector2(w * 0.36f, w * 0.2f), Leaf.WithAlpha(alpha), default, 0, order + 1);
            return node;
        }

        /// Fleur éclose : dix pétales, cœur crème, pistil orangé.
        public static Transform Flower(Transform parent, Vector2 pos, float w, int order, Color? petal = null)
        {
            var node = RenderShared.Node("Flower", parent, pos);
            var pc = petal ?? GoldColor;
            float r = w * 0.3f;
            for (int i = 0; i < 10; i++)
            {
                float a = i * Mathf.PI / 5f;
                ShapeView.Circle("Petal", node, Units.P(Mathf.Cos(a) * r * 0.98f, Mathf.Sin(a) * r * 0.98f), r * 0.26f, pc.WithAlpha(0.9f), default, 0, order);
            }
            ShapeView.Circle("Heart", node, Vector2.zero, r, new Color(1, 0.97f, 0.88f), default, 0, order + 1);
            ShapeView.Circle("Core", node, Vector2.zero, r * 0.47f, new Color(0.93f, 0.62f, 0.15f), default, 0, order + 2);
            return node;
        }

        /// Petite horloge (temps restant).
        public static Transform Clock(Transform parent, Vector2 pos, float w, Color color, int order)
        {
            var node = RenderShared.Node("Clock", parent, pos);
            ShapeView.Circle("Ring", node, Vector2.zero, w * 0.45f, Color.clear, color, w * 0.11f, order);
            StrokeView.Create("Hands", node, new[] { Units.P(0, w * 0.3f), Vector2.zero, Units.P(w * 0.22f, 0) }, color, w * 0.11f, order);
            return node;
        }

        /// Pastille dorée « ×N ».
        public static Transform PowerBadge(Transform parent, Vector2 pos, int power, int order, float fontPt = 16f)
        {
            var node = RenderShared.Node("PowerBadge", parent, pos);
            var label = TenGoText.Create(node, "×" + power, Fonts.AvenirHeavy, fontPt, new Color(0.36f, 0.22f, 0), Vector2.zero, order + 1);
            label.Tmp.ForceMeshUpdate();
            float textW = label.Tmp.GetRenderedValues(false).x / Units.FromPoints(1f);
            float w = textW + 16f, h = fontPt + 10f;
            ShapeView.Rect("Bg", node, Vector2.zero, new Vector2(w, h), h / 2f, GoldColor, default, 0, order);
            return node;
        }

        /// Légère pulsation (si les animations sont actives).
        public static void Pulse(Transform node, float to = 1.08f)
        {
            if (!JuiceSettings.MotionEnabled) return;
            Tw.Run(node.gameObject, Tw.Forever(Tw.Seq(Tw.Scale(node, to, 0.8f), Tw.Scale(node, 1f, 0.8f))));
        }
    }
}
