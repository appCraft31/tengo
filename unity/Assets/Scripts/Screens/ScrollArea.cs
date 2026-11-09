using System.Collections.Generic;
using TenGo.Rendering;
using TMPro;
using UnityEngine;

namespace TenGo.Screens
{
    /// Page défilante verticale (contenu sous un `SKCropNode`, `scrollOffset`
    /// des écrans iOS), au doigt : seuil de 8 pt avant de défiler (un tap
    /// reste un tap), puis inertie et rebond aux bords à la manière
    /// d'`UIScrollView` (décélération 0,998 par milliseconde, élastique 0,55).
    ///
    /// Le contenu part de y = 0 (haut de page) et descend en y négatif ;
    /// `Offset` = 0 en haut de page, `MaxOffset` en bas.
    public sealed class ScrollArea
    {
        /// Distance avant qu'un appui devienne un défilement (`abs(dy) > 8`).
        public const float DragThreshold = 8f;
        /// `UIScrollView.DecelerationRate.normal`, par milliseconde.
        public const float Deceleration = 0.998f;
        const float Elastic = 0.55f;

        public readonly Transform Content;
        public float Top { get; private set; }
        public float Bottom { get; private set; }
        public float Left { get; private set; }
        public float Right { get; private set; }
        public float ContentHeight;
        /// Défilement verrouillé (tutoriel d'achat).
        public bool Locked;

        public float Offset { get; private set; }
        public bool IsDragging { get; private set; }
        public float Velocity { get; private set; }
        public float MaxOffset => Mathf.Max(0f, ContentHeight - (Top - Bottom));

        readonly List<TextMeshPro> _texts = new();
        Vector4 _clip;
        bool _tracking;
        float _startY, _startOffset, _lastY, _lastT, _time;

        public ScrollArea(Transform parent, float top, float bottom, float left, float right, int order = 0)
        {
            Content = RenderShared.Node("ScrollContent", parent);
            SetViewport(top, bottom, left, right);
        }

        public void SetViewport(float top, float bottom, float left, float right)
        {
            Top = top; Bottom = bottom; Left = left; Right = right;
            _clip = Clipping.WorldRect(Rect.MinMaxRect(left, bottom, right, top));
            Place();
        }

        public bool ViewportContains(Vector2 pt) => pt.y <= Top && pt.y >= Bottom && pt.x >= Left && pt.x <= Right;

        /// À rappeler après chaque (re)construction du contenu.
        public void ApplyClip()
        {
            Clipping.Apply(Content, _clip, _texts);
            Place();
        }

        public void Clear()
        {
            for (int i = Content.childCount - 1; i >= 0; i--) RenderShared.Destroy(Content.GetChild(i).gameObject);
            _texts.Clear();
        }

        /// Place un défilement (borné), sans inertie.
        public void SetOffset(float offset)
        {
            Offset = Mathf.Clamp(offset, 0f, MaxOffset);
            Velocity = 0;
            Place();
        }

        /// Amène `contentY` (coordonnée du contenu, ≤ 0) en haut de la zone visible.
        public void ScrollTo(float contentY, float margin = 6f) => SetOffset(-contentY - margin);

        void Place()
        {
            if (Content == null) return;
            Content.localPosition = Units.P(0, Top + Offset);
            if (_texts.Count > 0) Clipping.UpdateTexts(_texts, _clip);
        }

        // MARK: - Geste

        public void Begin(Vector2 pt)
        {
            _tracking = ViewportContains(pt);
            IsDragging = false;
            if (!_tracking) return;
            _startY = _lastY = pt.y;
            _startOffset = Offset;
            _lastT = _time;
            // Un appui arrête l'inertie, comme UIScrollView.
            Velocity = 0;
            if (Offset < 0 || Offset > MaxOffset) { Offset = Mathf.Clamp(Offset, 0, MaxOffset); Place(); }
            _startOffset = Offset;
        }

        public void Drag(Vector2 pt)
        {
            if (!_tracking || Locked) return;
            float dy = pt.y - _startY;
            if (!IsDragging && Mathf.Abs(dy) > DragThreshold) IsDragging = true;
            if (!IsDragging) return;
            float dt = Mathf.Max(1e-3f, _time - _lastT);
            float v = (pt.y - _lastY) / dt;
            Velocity = Mathf.Lerp(Velocity, v, 0.6f);
            _lastY = pt.y;
            _lastT = _time;
            float raw = _startOffset + dy;
            float max = MaxOffset;
            // Au-delà des bords, le contenu suit le doigt à moitié (rebond).
            if (raw < 0) raw *= Elastic;
            else if (raw > max) raw = max + (raw - max) * Elastic;
            Offset = raw;
            Place();
        }

        public void End(Vector2 pt)
        {
            if (!_tracking) return;
            _tracking = false;
            // Doigt immobile depuis plus de 0,1 s : pas d'élan.
            if (_time - _lastT > 0.1f) Velocity = 0;
            if (!IsDragging) Velocity = 0;
            IsDragging = false;
        }

        public void Cancel()
        {
            _tracking = false;
            IsDragging = false;
        }

        public void Tick(float dt)
        {
            _time += dt;
            if (_tracking) return;
            float max = MaxOffset;
            bool moved = false;
            if (Mathf.Abs(Velocity) > 1f)
            {
                Offset += Velocity * dt;
                Velocity *= Mathf.Pow(Deceleration, dt * 1000f);
                // Hors des bords, l'élan s'éteint vite.
                if (Offset < 0 || Offset > max) Velocity *= Mathf.Pow(0.2f, dt * 10f);
                moved = true;
            }
            else Velocity = 0;
            if (Offset < 0 || Offset > max)
            {
                float target = Offset < 0 ? 0 : max;
                if (Mathf.Abs(Velocity) < 60f)
                {
                    // Retour au bord en ~0,3 s (ressort amorti).
                    Offset = Mathf.Lerp(Offset, target, 1f - Mathf.Pow(0.0005f, dt));
                    if (Mathf.Abs(Offset - target) < 0.3f) Offset = target;
                    Velocity = 0;
                }
                moved = true;
            }
            if (moved) Place();
        }
    }
}
