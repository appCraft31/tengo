using System;
using TenGo.Fx;
using TenGo.Rendering;
using UnityEngine;

namespace TenGo.Game
{
    /// Coach-mark (CoachMarkOverlay.swift) : voile sombre, cible remontée
    /// au-dessus du voile et cerclée d'un halo pulsant, bulle d'explication
    /// au-dessus de la cible si elle est dans la moitié basse (au-dessous sinon).
    public sealed class CoachMarkOverlay
    {
        public enum TouchResult { Target, Dismissed, Ignored }

        const int ScrimOrder = 900;
        const int TargetShift = 900;

        public bool BlocksOutsideTaps;
        public Action<TouchResult> OnDismiss;

        readonly Transform _root;
        readonly Transform _target;
        readonly Transform _halo;
        Rect _targetFrame;
        bool _closed;

        public Transform Root => _root;
        public Rect TargetFrame => _targetFrame;

        CoachMarkOverlay(Transform parent, Transform target, string text, Vector2 sceneSizePt)
        {
            _target = target;
            _root = RenderShared.Node("CoachMark", parent);
            FxAlpha.Of(_root.gameObject);
            _targetFrame = OrderShift.BoundsPt(target);
            OrderShift.Shift(target, TargetShift);

            var scrim = ShapeView.Rect("Scrim", _root, Vector2.zero, sceneSizePt * 2, 0, Colors.White(0, 0.45f), default, 0, ScrimOrder);
            FxAlpha.Of(scrim.gameObject).Alpha = 0;
            Tw.Run(scrim.gameObject, Tw.FadeIn(scrim.gameObject, 0.2f));

            const float pad = 14;
            var f = _targetFrame;
            var haloSize = new Vector2(f.width + pad * 2, f.height + pad * 2);
            _halo = RenderShared.Node("Halo", _root, Units.P(f.center.x, f.center.y));
            ShapeView.Rect("Ring", _halo, Vector2.zero, haloSize, Mathf.Min(haloSize.x, haloSize.y) / 2, Colors.White(1, 0.10f),
                Colors.White(1, 0.9f), 3, ScrimOrder + 1);
            Tw.Run(_halo.gameObject, Tw.Forever(Tw.Seq(Tw.Scale(_halo, 1.06f, 0.55f), Tw.Scale(_halo, 1f, 0.55f))), "pulse");

            bool above = f.center.y <= 0;
            var label = TenGoText.Create(null, text, Fonts.AvenirDemiBold, 19, Colors.White(0.25f, 1), Vector2.zero, ScrimOrder + 5);
            label.Wrap(250);
            float lw = Mathf.Min(250, label.WidthPt()), lh = label.HeightPt();
            const float padX = 22, padY = 16, gap = 34;
            var bubbleSize = new Vector2(lw + padX * 2, lh + padY * 2);
            float bubbleY = above ? f.yMax + pad + gap + bubbleSize.y / 2 : f.yMin - pad - gap - bubbleSize.y / 2;
            var fill = new Color(0.99f, 0.97f, 0.94f, 1);
            var bubble = RenderShared.Node("Bubble", _root, Units.P(0, bubbleY));
            ShapeView.Rect("Bg", bubble, Vector2.zero, bubbleSize, 18, fill, Colors.White(0.75f, 0.5f), 1, ScrimOrder + 3);
            label.transform.SetParent(bubble, false);
            label.transform.localPosition = Vector3.zero;

            // Pointe : losange glissé sous la bulle, dont seule la moitié tournée vers la cible dépasse.
            var arrow = RenderShared.Node("Arrow", _root,
                Units.P(f.center.x, above ? bubbleY - bubbleSize.y / 2 : bubbleY + bubbleSize.y / 2));
            var diamond = ShapeView.Rect("Tip", arrow, Vector2.zero, new Vector2(17, 17), 2, fill, default, 0, ScrimOrder + 2);
            diamond.transform.localRotation = Quaternion.Euler(0, 0, 45);
            diamond.transform.localScale = new Vector3(1f, 1.2f, 1);

            foreach (var node in new[] { _halo, bubble, arrow })
            {
                FxAlpha.Of(node.gameObject).Alpha = 0;
                Tw.Run(node.gameObject, Tw.Seq(Tw.Wait(0.1f), Tw.FadeIn(node.gameObject, 0.25f)), "appear");
            }
        }

        public static CoachMarkOverlay Present(Transform parent, Transform target, string text, Vector2 sceneSizePt) =>
            new(parent, target, text, sceneSizePt);

        public TouchResult HandleTouch(Vector2 pt)
        {
            bool hit = _targetFrame.Contains(pt);
            if (!hit && BlocksOutsideTaps)
            {
                Tw.Run(_halo.gameObject, Tw.Seq(Tw.Scale(_halo, 1.18f, 0.10f), Tw.Scale(_halo, 1f, 0.16f)), "nudge");
                return TouchResult.Ignored;
            }
            var result = hit ? TouchResult.Target : TouchResult.Dismissed;
            Dismiss();
            OnDismiss?.Invoke(result);
            return result;
        }

        public void Dismiss()
        {
            if (_closed) return;
            _closed = true;
            if (_target != null) OrderShift.Shift(_target, -TargetShift);
            if (_root != null)
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(_root.gameObject);
                else UnityEngine.Object.DestroyImmediate(_root.gameObject);
            }
        }
    }
}
