using System;
using TenGo.Fx;
using TenGo.Rendering;
using UnityEngine;

namespace TenGo.Ui
{
    /// Transition entre écrans (SceneTransition.swift) : fondu qui passe par le
    /// fond du thème actif, jamais par le noir. Un voile plein écran de la
    /// couleur du fond monte en `duration / 2`, l'écran change sous le voile,
    /// puis le voile redescend ; les deux écrans restent animés. Les onglets,
    /// eux, ne s'animent pas (`TabBar.present`).
    public static class SceneFader
    {
        public const float DefaultDuration = 0.28f;

        /// `swap` est appelé à mi-parcours, voile opaque. `cover` : surface en points.
        public static Transform Fade(Transform root, Vector2 coverPt, Action swap, float duration = DefaultDuration)
        {
            var veil = ShapeView.Rect("Transition", root, Vector2.zero, coverPt * 1.2f, 0, Look.Background, default, 0, Layers.Transition);
            var go = veil.gameObject;
            FxAlpha.Of(go).Alpha = 0f;
            Tw.Run(go, Tw.Seq(
                Tw.FadeIn(go, duration / 2f),
                Tw.Call(() => swap?.Invoke()),
                Tw.Call(() => { if (veil != null) veil.Fill = Look.Background; }),
                Tw.FadeOut(go, duration / 2f),
                Tw.Remove(go)));
            return veil.transform;
        }
    }
}
