using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TenGo.Fx
{
    /// Petit système d'animations, équivalent des `SKAction` (voir CONVENTIONS.md).
    ///
    /// ```
    /// Tw.Run(ring, Tw.Seq(
    ///     Tw.Par(Tw.Scale(ring.transform, 4.5f, 0.5f), Tw.FadeOut(ring.gameObject, 0.5f)),
    ///     Tw.Remove(ring.gameObject)));
    /// Tw.Run(enemy, Tw.Forever(Tw.Seq(
    ///     Tw.Scale(t, 1.18f, 2f).Ease(TwEase.EaseInEaseOut),
    ///     Tw.Scale(t, 0.82f, 2f).Ease(TwEase.EaseInEaseOut))), "breathe");
    /// ```
    /// Les durées sont en secondes, les déplacements en unités locales (voir `Units`).
    public static class Tw
    {
        public static readonly TweenRunner Runner = new();

        // MARK: - Lancer, arrêter

        /// Lance `tween` sur `owner`. Avec une clé, remplace l'action de même clé
        /// (`run(_:withKey:)`). L'action s'arrête si `owner` est détruit.
        public static Tween Run(Object owner, Tween tween, string key = null)
        {
            FxTime.EnsureDriver();
            Runner.Run(owner, tween, key);
            return tween;
        }

        /// `removeAction(forKey:)`, ou `removeAllActions()` sans clé.
        public static void Kill(Object owner, string key = null) => Runner.Kill(owner, key);

        /// Une action de cette clé tourne-t-elle sur `owner` ?
        public static bool Has(Object owner, string key = null) => Runner.Has(owner, key);

        /// `node.speed` : 0 fige les actions de `owner` (gel du pulsar), 1 les relance.
        public static void SetSpeed(Object owner, float speed) => Runner.SetSpeed(owner, speed);

        // MARK: - Composition

        public static Tween Seq(params Tween[] items) => new SeqTween(items);
        public static Tween Par(params Tween[] items) => new ParTween(items);
        public static Tween Forever(Tween t) => new RepeatTween(t, -1);
        public static Tween Repeat(Tween t, int count) => new RepeatTween(t, count);
        public static Tween Wait(float d) => new WaitTween(d);
        public static Tween Call(Action a) => new CallTween(a);

        /// `removeFromParent()`.
        public static Tween Remove(GameObject go) => new CallTween(() =>
        {
            if (go == null) return;
            if (Application.isPlaying) Object.Destroy(go); else Object.DestroyImmediate(go);
        });

        // MARK: - Valeurs

        public static ValueTween<float> To(Func<float> get, Action<float> set, float to, float d, TwEase ease = TwEase.Linear) =>
            new(get, set, Mathf.LerpUnclamped, to, d, ease);

        public static ValueTween<Vector3> To(Func<Vector3> get, Action<Vector3> set, Vector3 to, float d, TwEase ease = TwEase.Linear) =>
            new(get, set, Vector3.LerpUnclamped, to, d, ease);

        public static ValueTween<Color> To(Func<Color> get, Action<Color> set, Color to, float d, TwEase ease = TwEase.Linear) =>
            new(get, set, Color.LerpUnclamped, to, d, ease);

        // MARK: - Nœuds

        /// `fadeAlpha(to:)` sur l'`FxAlpha` du nœud (ajouté si absent).
        public static ValueTween<float> Fade(GameObject go, float to, float d)
        {
            var a = FxAlpha.Of(go);
            return To(() => a.Alpha, v => { if (a != null) a.Alpha = v; }, to, d);
        }

        public static ValueTween<float> FadeIn(GameObject go, float d) => Fade(go, 1f, d);
        public static ValueTween<float> FadeOut(GameObject go, float d) => Fade(go, 0f, d);

        /// `scale(to:)` : échelle absolue, x et y.
        public static ValueTween<Vector3> Scale(Transform t, float to, float d) =>
            To(() => t.localScale, v => { if (t != null) t.localScale = v; }, new Vector3(to, to, 1f), d);

        /// `scaleX(to:)`.
        public static ValueTween<float> ScaleX(Transform t, float to, float d) =>
            To(() => t.localScale.x, v => { if (t != null) { var s = t.localScale; s.x = v; t.localScale = s; } }, to, d);

        /// `scaleY(to:)`.
        public static ValueTween<float> ScaleY(Transform t, float to, float d) =>
            To(() => t.localScale.y, v => { if (t != null) { var s = t.localScale; s.y = v; t.localScale = s; } }, to, d);

        /// `move(to:)` : position locale absolue.
        public static ValueTween<Vector3> MoveTo(Transform t, Vector2 to, float d) =>
            To(() => t.localPosition, v => { if (t != null) t.localPosition = v; },
               new Vector3(to.x, to.y, t.localPosition.z), d);

        /// `moveBy(x:y:)` : déplacement relatif, cumulable.
        public static MoveByTween MoveBy(Transform t, Vector2 delta, float d) => new(t, delta, d);

        /// `moveTo(y:)` : ordonnée locale absolue, l'abscisse reste libre.
        public static ValueTween<float> MoveToY(Transform t, float y, float d) =>
            To(() => t.localPosition.y, v => { if (t != null) { var p = t.localPosition; p.y = v; t.localPosition = p; } }, y, d);

        /// `rotate(byAngle:)` : rotation relative (radians, sens trigonométrique comme SpriteKit).
        public static Tween RotateBy(Transform t, float radians, float d)
        {
            float done = 0f;
            return Custom(d, p =>
            {
                if (t == null) return;
                t.localRotation *= Quaternion.Euler(0, 0, (p - done) * radians * Mathf.Rad2Deg);
                done = p;
            }, () => done = 0f);
        }

        /// `customAction(withDuration:)` : `step` reçoit la progression 0-1 (linéaire).
        public static Tween Custom(float d, Action<float> step, Action restart = null) => new CustomTween(d, step, restart);
    }
}
