using UnityEngine;

namespace TenGo.App
{
    /// Cadrage d'une scène SpriteKit 750 × 1334 en `aspectFill` : l'échelle est
    /// `max(largeur / 750, hauteur / 1334)`, si bien qu'un téléphone plus haut
    /// que 16:9 voit toute la hauteur et perd des bords sur les côtés, une
    /// tablette voit toute la largeur et perd du haut et du bas.
    ///
    /// Les marges sûres sont rendues en unités de scène, avec le plancher de
    /// 47 points d'interface d'iOS (`max(view.safeAreaInsets.top, 47) / scale`).
    public readonly struct SceneGeometry
    {
        public const float RefWidth = 750f, RefHeight = 1334f;
        /// Plancher de la marge haute d'iOS, en points d'interface (`MenuScene.setupUI`).
        public const float TopInsetFloorPt = 47f;

        /// Définition de l'écran en pixels.
        public readonly int PixelWidth, PixelHeight;
        /// Pixels par point d'interface (3 sur un iPhone récent).
        public readonly float PixelsPerPoint;
        /// Marges sûres en pixels (haut, bas).
        public readonly float SafeTopPx, SafeBottomPx;

        public SceneGeometry(int pxW, int pxH, float pixelsPerPoint, float safeTopPx, float safeBottomPx)
        {
            PixelWidth = Mathf.Max(1, pxW);
            PixelHeight = Mathf.Max(1, pxH);
            PixelsPerPoint = Mathf.Max(0.5f, pixelsPerPoint);
            SafeTopPx = Mathf.Max(0f, safeTopPx);
            SafeBottomPx = Mathf.Max(0f, safeBottomPx);
        }

        /// Pixels par unité de scène (`scale` d'aspectFill).
        public float PixelsPerScene => Mathf.Max(PixelWidth / RefWidth, PixelHeight / RefHeight);
        /// Surface visible, en unités de scène.
        public float VisibleWidth => PixelWidth / PixelsPerScene;
        public float VisibleHeight => PixelHeight / PixelsPerScene;
        public float VisibleTop => VisibleHeight / 2f;
        public float VisibleBottom => -VisibleHeight / 2f;
        /// Marge haute d'iOS, plancher compris, en unités de scène.
        public float TopInset => Mathf.Max(SafeTopPx, TopInsetFloorPt * PixelsPerPoint) / PixelsPerScene;
        /// Marge basse (barre de gestes), en unités de scène.
        public float BottomInset => SafeBottomPx / PixelsPerScene;
        /// Demi-hauteur de la caméra orthographique (1 unité = 100 unités de scène).
        public float OrthoSize => VisibleHeight / 200f;

        /// Pixel de l'écran (origine en bas à gauche) → point de scène (origine au centre).
        /// Indépendant de la caméra : les secousses ne déplacent jamais les zones tactiles.
        public Vector2 ScreenToScene(Vector2 px) =>
            new((px.x / PixelWidth - 0.5f) * VisibleWidth, (px.y / PixelHeight - 0.5f) * VisibleHeight);

        /// Écran courant de l'appareil.
        public static SceneGeometry Current()
        {
            var safe = Screen.safeArea;
            float dpi = Screen.dpi;
#if UNITY_ANDROID && !UNITY_EDITOR
            float ppp = dpi > 0 ? dpi / 160f : 3f;
#elif UNITY_IOS && !UNITY_EDITOR
            float ppp = dpi > 0 ? Mathf.Round(dpi / 163f) : 3f;
#else
            float ppp = dpi > 0 ? Mathf.Max(1f, dpi / 160f) : 2f;
#endif
            return new SceneGeometry(Screen.width, Screen.height, ppp, Screen.height - safe.yMax, safe.yMin);
        }

        /// iPhone 17 Pro Max (1320 × 2868 px, 3 px/pt, Dynamic Island 62 pt, barre de gestes 34 pt) :
        /// l'appareil des captures iOS de référence.
        public static SceneGeometry IPhone17ProMax => new(1320, 2868, 3f, 62f * 3f, 34f * 3f);

        /// Tablette Android 10 pouces en portrait (1600 × 2560 px, 2 px/dp, barre d'état 24 dp,
        /// barre de navigation par gestes 24 dp) : format des captures de tablette de Google Play.
        public static SceneGeometry AndroidTablet10 => new(1600, 2560, 2f, 24f * 2f, 24f * 2f);

        public bool SameAs(SceneGeometry o) =>
            PixelWidth == o.PixelWidth && PixelHeight == o.PixelHeight &&
            Mathf.Abs(SafeTopPx - o.SafeTopPx) < 0.5f && Mathf.Abs(SafeBottomPx - o.SafeBottomPx) < 0.5f;

        public override string ToString() =>
            $"{PixelWidth}×{PixelHeight} px, visible {VisibleWidth:0}×{VisibleHeight:0}, marge haute {TopInset:0.#}, basse {BottomInset:0.#}";
    }
}
