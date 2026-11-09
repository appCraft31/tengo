using System;
using TenGo.Fx;
using TenGo.Haptics;
using TenGo.Rendering;
using UnityEngine;

namespace TenGo.Ui
{
    /// Bouton dessiné dans la scène (comme les `SKShapeNode` nommés des écrans
    /// iOS) : forme en relief (`Relief.Raise`), libellé ou icône, zone tactile.
    /// L'action part au toucher, avec le retour léger de `tapFeedback`.
    [DisallowMultipleComponent]
    public sealed class UiButton : MonoBehaviour
    {
        public ShapeView Background { get; private set; }
        public Relief.Parts ReliefParts { get; private set; }
        public TenGoText Label { get; private set; }
        public VectorIcon Icon { get; private set; }
        /// Zone tactile en points (centrée), au moins 44 × 44.
        public Vector2 HitSizePt { get; set; }
        public Action OnTap;
        public bool Interactable = true;
        /// Faux pour un bouton dont l'action vibre déjà elle-même (« Réclamer »).
        public bool Haptic = true;

        /// Bouton capsule (`rectOf:cornerRadius:`) : `fill` nul = `Relief.Surface`.
        /// `depthPt` = 0 : sans relief, avec un filet `stroke`.
        public static UiButton Create(Transform parent, string name, Vector2 localPos, Vector2 sizePt, float cornerPt,
            Color? fill, string label, NunitoWeight weight, float fontPt, Color textColor, int sortingOrder,
            Action onTap, float depthPt = 5f, Color? stroke = null, float strokeWidthPt = 0f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            FxAlpha.Of(go);
            var b = go.AddComponent<UiButton>();
            b.Background = ShapeView.Rect("Bg", go.transform, Vector2.zero, sizePt, cornerPt, fill ?? Relief.Surface,
                stroke ?? Color.clear, strokeWidthPt, sortingOrder);
            if (depthPt > 0f) b.ReliefParts = Relief.Raise(b.Background, depthPt, surface: fill == null);
            if (label != null)
                b.Label = TenGoText.Create(go.transform, label, weight, fontPt, textColor, Vector2.zero, sortingOrder + 2);
            b.HitSizePt = sizePt;
            b.OnTap = onTap;
            return b;
        }

        /// Bouton rond à icône (accueil, réglages, boosters) : disque `fill` en relief.
        public static UiButton CreateRound(Transform parent, string name, Vector2 localPos, float radiusPt, Color fill,
            VectorIconName icon, float iconPt, Color iconColor, int sortingOrder, Action onTap, float depthPt = 4f)
        {
            var b = Create(parent, name, localPos, new Vector2(radiusPt * 2, radiusPt * 2), radiusPt, fill, null,
                NunitoWeight.Bold, 0, Color.clear, sortingOrder, onTap, depthPt);
            b.Icon = VectorIcon.Create(b.transform, icon, Vector2.zero, iconPt, iconColor, sortingOrder + 2);
            return b;
        }

        /// Le point (unités monde) tombe-t-il dans la zone tactile ?
        public bool Hit(Vector2 world)
        {
            if (!Interactable || !isActiveAndEnabled) return false;
            if (FxAlpha.Inherited(transform) < 0.05f) return false;
            Vector2 local = transform.InverseTransformPoint(world);
            float hx = Units.FromPoints(Mathf.Max(HitSizePt.x, 44f) * 0.5f);
            float hy = Units.FromPoints(Mathf.Max(HitSizePt.y, 44f) * 0.5f);
            return Mathf.Abs(local.x) <= hx && Mathf.Abs(local.y) <= hy;
        }

        public void Tap()
        {
            if (Haptic && OnTap != null) TapFeedback.Play();
            OnTap?.Invoke();
        }
    }
}
