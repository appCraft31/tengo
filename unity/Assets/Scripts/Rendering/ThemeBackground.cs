using TenGo.Core;
using TenGo.Fx;
using UnityEngine;

namespace TenGo.Rendering
{
    /// Ambiance animée de chaque thème (ThemeBackground.swift).
    public enum ThemeEffect { Bubbles, Waves, Stars, Leaves, Sand, Moon, Candy }

    /// Fond animé du thème : bulles pastel (défaut), vagues (océan), étoiles et
    /// planètes (espace), feuilles (forêt), soleil et poussières (désert),
    /// étoiles et lune (nuit), bulles et étincelles (bonbon). 100 % vectoriel,
    /// derrière le jeu (z −1). La surface est celle de la scène (750 × 1334).
    /// Les tirages aléatoires passent par `rng` : graine fixe en capture.
    public static class ThemeBackground
    {
        public static ThemeEffect EffectOf(Theme theme) => theme.Id switch
        {
            "ocean" => ThemeEffect.Waves,
            "space" => ThemeEffect.Stars,
            "forest" => ThemeEffect.Leaves,
            "desert" => ThemeEffect.Sand,
            "night" => ThemeEffect.Moon,
            "candy" => ThemeEffect.Candy,
            _ => ThemeEffect.Bubbles,
        };

        public static Transform Make(Transform parent, Theme theme, Vector2 sizePt, System.Random rng = null, int order = Layers.Background)
        {
            rng ??= new System.Random();
            var node = RenderShared.Node("ThemeBackground", parent);
            var ctx = new Ctx { Node = node, Theme = theme, W = sizePt.x, H = sizePt.y, Rng = rng, Order = order };
            switch (EffectOf(theme))
            {
                case ThemeEffect.Bubbles: Bubbles(ctx); break;
                case ThemeEffect.Waves: Waves(ctx); break;
                case ThemeEffect.Stars: Stars(ctx, 70, false); Planets(ctx); break;
                case ThemeEffect.Leaves: Leaves(ctx); break;
                case ThemeEffect.Sand: Sand(ctx); break;
                case ThemeEffect.Moon: Stars(ctx, 45, true); break;
                case ThemeEffect.Candy: Candy(ctx); break;
            }
            return node;
        }

        sealed class Ctx
        {
            public Transform Node;
            public Theme Theme;
            public float W, H;
            public System.Random Rng;
            public int Order;
            public Color Bubble(int i) => Colors.FromRgb(Theme.Bubbles[i]);
            public float R(float a, float b) => a + (float)Rng.NextDouble() * (b - a);
            public bool Coin() => Rng.Next(2) == 0;
        }

        /// Va-et-vient vertical sans fin (`moveBy(y:)` aller-retour en easeInEaseOut).
        static void Bob(Transform t, float dyPt, float duration)
        {
            Tw.Run(t.gameObject, Tw.Forever(Tw.Seq(
                Tw.MoveBy(t, Units.P(0, dyPt), duration).Ease(TwEase.EaseInEaseOut),
                Tw.MoveBy(t, Units.P(0, -dyPt), duration).Ease(TwEase.EaseInEaseOut))));
        }

        // MARK: - Bulles (défaut)

        static readonly (float r, float x, float y, int c, float d)[] BubbleConfigs =
        {
            (88, -280, 480, 0, 7.2f), (62, 260, 350, 4, 9.5f), (110, -180, -150, 6, 11.0f),
            (74, 310, -320, 1, 8.3f), (96, -60, 560, 3, 10.1f), (54, 200, -500, 7, 6.8f),
            (80, -320, -480, 2, 12.4f), (68, 80, 200, 5, 9.0f), (50, -200, -30, 8, 7.6f),
        };

        static void Bubbles(Ctx c)
        {
            foreach (var cfg in BubbleConfigs)
            {
                var b = ShapeView.Circle("Bubble", c.Node, Units.P(cfg.x, cfg.y), cfg.r, c.Bubble(cfg.c).WithAlpha(0.18f), default, 0, c.Order);
                Bob(b.transform, 18, cfg.d);
            }
        }

        // MARK: - Vagues (océan)

        static void Waves(Ctx c)
        {
            var layers = new (float y, float amp, float len, int c, float alpha, float speed)[]
            {
                (c.H * 0.34f, 16, 240, 8, 0.16f, 11),
                (c.H * 0.10f, 22, 300, 5, 0.18f, 14),
                (-c.H * 0.12f, 18, 200, 2, 0.16f, 9),
                (-c.H * 0.34f, 26, 340, 6, 0.20f, 16),
            };
            foreach (var l in layers)
            {
                var wave = WaveView.Create(c.Node, Units.P(0, l.y), c.W * 2.4f, l.amp, l.len, 4, c.Bubble(l.c).WithAlpha(l.alpha), c.Order);
                var t = wave.transform;
                // Défilement d'une longueur d'onde puis retour instantané : raccord invisible.
                Tw.Run(wave.gameObject, Tw.Forever(Tw.Seq(Tw.MoveBy(t, Units.P(-l.len, 0), l.speed), Tw.MoveBy(t, Units.P(l.len, 0), 0))));
                Bob(t, 8, l.speed * 0.5f);
            }
        }

        // MARK: - Étoiles (espace, nuit)

        static void Stars(Ctx c, int count, bool withMoon)
        {
            for (int i = 0; i < count; i++)
            {
                float r = c.R(0.8f, 2.4f);
                // Surtout blanches, parfois une teinte du thème.
                var color = c.Coin() && c.Coin() ? c.Bubble(c.Rng.Next(c.Theme.Bubbles.Count)).WithAlpha(0.9f) : Colors.White(1, 0.95f);
                var star = ShapeView.Circle("Star", c.Node, Units.P(c.R(-c.W / 2, c.W / 2), c.R(-c.H / 2, c.H / 2)), r, color, default, 0, c.Order);
                var fa = FxAlpha.Of(star.gameObject);
                fa.Alpha = c.R(0.3f, 0.9f);
                float lo = c.R(0.2f, 0.45f), hi = c.R(0.8f, 1f), dur = c.R(0.8f, 2.6f);
                Tw.Run(star.gameObject, Tw.Forever(Tw.Seq(Tw.Fade(star.gameObject, lo, dur), Tw.Fade(star.gameObject, hi, dur))), "twinkle");
                c.R(0, 2);   // tirage du wait(0…2) d'iOS, sans effet : gardé pour la suite des tirages
            }
            if (withMoon) Moon(c);
        }

        /// Planètes douces (Espace), vers les bords pour épargner la grille.
        static void Planets(Ctx c)
        {
            var planets = new (float x, float y, float r, int c, bool ring)[]
            {
                (-c.W * 0.30f, c.H * 0.31f, 58, 5, true),
                (c.W * 0.31f, -c.H * 0.30f, 44, 2, false),
                (c.W * 0.33f, c.H * 0.42f, 26, 3, false),
            };
            foreach (var p in planets)
            {
                var planet = RenderShared.Node("Planet", c.Node, Units.P(p.x, p.y));
                var b = c.Bubble(p.c);
                ShapeView.Circle("Disc", planet, Vector2.zero, p.r, b.WithAlpha(0.55f), b.WithAlpha(0.7f), 1, c.Order);
                ShapeView.Circle("Glow", planet, Units.P(-p.r * 0.3f, p.r * 0.3f), p.r * 0.42f, Colors.White(1, 0.12f), default, 0, c.Order + 1);
                if (p.ring)
                {
                    var ring = ShapeView.Ellipse("Ring", planet, Vector2.zero, new Vector2(p.r * 3f, p.r * 1.05f), Color.clear, Colors.White(0.88f, 0.4f), 4, c.Order + 2);
                    ring.transform.localRotation = Quaternion.Euler(0, 0, -0.35f * Mathf.Rad2Deg);
                }
                Bob(planet, 12, 7.5f);
            }
        }

        static void Moon(Ctx c)
        {
            const float moonR = 38;
            float cx = c.W * 0.26f, cy = c.H * 0.30f;
            ShapeView.Circle("Moon", c.Node, Units.P(cx, cy), moonR, Colors.White(0.95f, 0.92f), default, 0, c.Order);
            ShapeView.Circle("MoonHalo", c.Node, Units.P(cx, cy), moonR + 14, Colors.White(1, 0.08f), default, 0, c.Order - 10);
            // Croissant : disque couleur du fond décalé qui « creuse » la lune.
            ShapeView.Circle("Carve", c.Node, Units.P(cx + 18, cy + 10), moonR, Colors.FromRgb(c.Theme.Background), default, 0, c.Order + 1);
        }

        // MARK: - Feuilles (forêt)

        static void Leaves(Ctx c)
        {
            const int count = 14;
            for (int i = 0; i < count; i++)
            {
                var leaf = ShapeView.Ellipse("Leaf", c.Node, Units.P(c.R(-c.W / 2, c.W / 2), c.R(-c.H / 2, c.H / 2)),
                    new Vector2(16, 9), c.Bubble(i % c.Theme.Bubbles.Count).WithAlpha(0.55f), default, 0, c.Order);
                var t = leaf.transform;
                t.localRotation = Quaternion.Euler(0, 0, c.R(0, Mathf.PI * 2) * Mathf.Rad2Deg);
                float fallDur = c.R(9, 16);
                var ctx = c;
                void FallLoop()
                {
                    if (t == null) return;
                    t.localPosition = Units.P(ctx.R(-ctx.W / 2, ctx.W / 2), ctx.H / 2 + 30);
                    Tw.Run(t.gameObject, Tw.Seq(Tw.MoveBy(t, Units.P(ctx.R(-40, 40), -(ctx.H + 60)), fallDur), Tw.Call(FallLoop)), "fall");
                }
                // Démarrage échelonné.
                Tw.Run(t.gameObject, Tw.Seq(Tw.Wait(i * 0.7f), Tw.Call(FallLoop)), "fall");
                float swayR = c.R(14, 28), swayD = c.R(1.6f, 2.8f);
                Tw.Run(t.gameObject, Tw.Forever(Tw.Seq(
                    Tw.MoveBy(t, Units.P(swayR, 0), swayD).Ease(TwEase.EaseInEaseOut),
                    Tw.MoveBy(t, Units.P(-swayR, 0), swayD).Ease(TwEase.EaseInEaseOut))));
                Tw.Run(t.gameObject, Tw.Forever(Tw.RotateBy(t, c.Coin() ? Mathf.PI : -Mathf.PI, c.R(3, 6))));
            }
        }

        // MARK: - Sable et soleil (désert)

        static void Sand(Ctx c)
        {
            float cx = c.W * 0.24f, cy = c.H * 0.32f;
            var accent = Colors.FromRgb(c.Theme.Accent);
            int k = 0;
            foreach (var rr in new[] { 110f, 80f, 54f })
            {
                ShapeView.Circle("Sun", c.Node, Units.P(cx, cy), rr, accent.WithAlpha(0.10f + k * 0.05f), default, 0, c.Order + k);
                k++;
            }
            for (int i = 0; i < 22; i++)
            {
                var mote = ShapeView.Circle("Mote", c.Node, Vector2.zero, c.R(2, 6), c.Bubble(i % c.Theme.Bubbles.Count).WithAlpha(0.22f), default, 0, c.Order + 3);
                mote.transform.localPosition = Units.P(c.R(-c.W / 2, c.W / 2), c.R(-c.H / 2, c.H / 2));
                float dx = c.R(20, 60), dur = c.R(6, 12);
                var t = mote.transform;
                Tw.Run(t.gameObject, Tw.Forever(Tw.Seq(
                    Tw.MoveBy(t, Units.P(dx, 8), dur).Ease(TwEase.EaseInEaseOut),
                    Tw.MoveBy(t, Units.P(-dx, -8), dur).Ease(TwEase.EaseInEaseOut))));
            }
        }

        // MARK: - Bonbon (bulles + étincelles)

        static void Candy(Ctx c)
        {
            Bubbles(c);
            for (int i = 0; i < 14; i++)
            {
                var sparkle = Sparkle(c.Node, Units.P(c.R(-c.W / 2, c.W / 2), c.R(-c.H / 2, c.H / 2)), Colors.White(1, 0.9f), c.Order + 1);
                sparkle.localScale = Vector3.one * c.R(0.5f, 1f);
                FxAlpha.Of(sparkle.gameObject).Alpha = 0f;
                float dur = c.R(0.7f, 1.6f);
                var go = sparkle.gameObject;
                Tw.Run(go, Tw.Seq(
                    Tw.Wait(c.R(0, 2.5f)),
                    Tw.Forever(Tw.Seq(
                        Tw.Par(Tw.Fade(go, 0.9f, dur), Tw.Scale(sparkle, 1.1f, dur)),
                        Tw.Par(Tw.Fade(go, 0f, dur), Tw.Scale(sparkle, 0.5f, dur))))));
            }
        }

        /// Croix fine de 14 pt (deux rectangles de 1,6 pt).
        static Transform Sparkle(Transform parent, Vector2 pos, Color color, int order)
        {
            var s = RenderShared.Node("Sparkle", parent, pos);
            const float arm = 7, thin = 1.6f;
            ShapeView.Rect("V", s, Vector2.zero, new Vector2(thin, arm * 2), 0, color, default, 0, order);
            ShapeView.Rect("H", s, Vector2.zero, new Vector2(arm * 2, thin), 0, color, default, 0, order);
            return s;
        }
    }
}
