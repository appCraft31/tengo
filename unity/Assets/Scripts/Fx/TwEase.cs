using UnityEngine;

namespace TenGo.Fx
{
    /// Courbes de `SKAction.timingMode`. SpriteKit ne documente pas la courbe
    /// exacte ; on prend les fonctions de Core Animation, qui s'en approchent :
    /// easeIn = Bézier (0,42 ; 0 ; 1 ; 1), easeOut = (0 ; 0 ; 0,58 ; 1),
    /// easeInEaseOut = (0,42 ; 0 ; 0,58 ; 1). Par défaut : linéaire, comme SpriteKit.
    public enum TwEase
    {
        Linear,
        EaseIn,
        EaseOut,
        EaseInEaseOut,
    }

    public static class TwEasing
    {
        /// Progression `t` (0-1) passée par la courbe.
        public static float Evaluate(TwEase ease, float t)
        {
            t = Mathf.Clamp01(t);
            switch (ease)
            {
                case TwEase.EaseIn: return CubicBezier(0.42f, 0f, 1f, 1f, t);
                case TwEase.EaseOut: return CubicBezier(0f, 0f, 0.58f, 1f, t);
                case TwEase.EaseInEaseOut: return CubicBezier(0.42f, 0f, 0.58f, 1f, t);
                default: return t;
            }
        }

        /// Bézier cubique (0,0)-(x1,y1)-(x2,y2)-(1,1) évaluée en x = `t`
        /// (Newton puis dichotomie, comme les fonctions de minutage de Core Animation).
        public static float CubicBezier(float x1, float y1, float x2, float y2, float t)
        {
            if (t <= 0f) return 0f;
            if (t >= 1f) return 1f;
            float cx = 3f * x1, bx = 3f * (x2 - x1) - cx, ax = 1f - cx - bx;
            float cy = 3f * y1, by = 3f * (y2 - y1) - cy, ay = 1f - cy - by;
            float SampleX(float s) => ((ax * s + bx) * s + cx) * s;
            float SampleY(float s) => ((ay * s + by) * s + cy) * s;
            float SlopeX(float s) => (3f * ax * s + 2f * bx) * s + cx;

            float u = t;
            for (int i = 0; i < 8; i++)
            {
                float err = SampleX(u) - t;
                if (Mathf.Abs(err) < 1e-6f) return SampleY(u);
                float d = SlopeX(u);
                if (Mathf.Abs(d) < 1e-6f) break;
                u -= err / d;
            }
            float lo = 0f, hi = 1f;
            u = t;
            for (int i = 0; i < 32; i++)
            {
                float x = SampleX(u);
                if (Mathf.Abs(x - t) < 1e-6f) break;
                if (x < t) lo = u; else hi = u;
                u = (lo + hi) * 0.5f;
            }
            return SampleY(u);
        }
    }
}
