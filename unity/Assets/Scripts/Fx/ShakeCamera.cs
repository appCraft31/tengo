using UnityEngine;

namespace TenGo.Fx
{
    /// Caméra de secousse (CameraShake.swift) : secousse amortie et zoom bref.
    /// Ne déplace que le rendu ; les touches de l'étape 5 se lisent en
    /// coordonnées de scène, donc la zone tactile ne bouge jamais.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class ShakeCamera : MonoBehaviour
    {
        /// Amplitude maximale tolérée (points) : au-delà, les chiffres deviennent illisibles.
        public const float MaxAmplitude = 10f;

        Camera _cam;
        Vector3 _rest;
        float _restSize;
        float _scale = 1f;

        /// Échelle courante (`camera.xScale`) : < 1 = zoom avant.
        public float Scale => _scale;
        public Vector3 Offset => transform.localPosition - _rest;

        public static ShakeCamera Attach(Camera cam)
        {
            var s = cam.GetComponent<ShakeCamera>();
            if (s == null) s = cam.gameObject.AddComponent<ShakeCamera>();
            s.Init();
            return s;
        }

        void Init()
        {
            _cam = GetComponent<Camera>();
            _rest = transform.localPosition;
            _restSize = _cam.orthographicSize;
            _scale = 1f;
        }

        /// À appeler si le cadrage de repos change (rotation, nouvel écran).
        public void SetRest(Vector3 position, float orthoSize)
        {
            _rest = position;
            _restSize = orthoSize;
            Apply(Vector2.zero);
            SetScale(1f);
        }

        void SetScale(float s)
        {
            _scale = s;
            if (_cam != null) _cam.orthographicSize = _restSize * s;
        }

        void Apply(Vector2 offsetPt) => transform.localPosition = _rest + (Vector3)Units.P(offsetPt.x, offsetPt.y);

        /// Secousse d'amplitude décroissante (points), repartant toujours du centre.
        public void Shake(float amplitude, float duration, System.Random rng = null)
        {
            if (!JuiceSettings.MotionEnabled) return;
            float amp = Mathf.Min(amplitude, MaxAmplitude);
            if (amp <= 0f || duration <= 0f) return;
            rng ??= new System.Random();
            Tw.Kill(gameObject, "shake");
            Apply(Vector2.zero);
            Tw.Run(gameObject, Tw.Seq(
                Tw.Custom(duration, p =>
                {
                    float damped = amp * Mathf.Max(0f, 1f - p);
                    if (damped <= 0.01f) { Apply(Vector2.zero); return; }
                    Apply(new Vector2(((float)rng.NextDouble() * 2f - 1f) * damped, ((float)rng.NextDouble() * 2f - 1f) * damped));
                }),
                Tw.Call(() => Apply(Vector2.zero))), "shake");
        }

        /// Zoom avant bref puis retour : l'image « avance » d'un cran.
        public void Punch(float scale)
        {
            if (!JuiceSettings.MotionEnabled || scale >= 1f) return;
            Tw.Kill(gameObject, "punch");
            Tw.Run(gameObject, Tw.Seq(
                Tw.To(() => _scale, SetScale, scale, 0.045f, TwEase.EaseOut),
                Tw.To(() => _scale, SetScale, 1f, 0.20f, TwEase.EaseOut),
                Tw.Call(() => SetScale(1f))), "punch");
        }

        public void ResetView()
        {
            Tw.Kill(gameObject, "shake");
            Tw.Kill(gameObject, "punch");
            Apply(Vector2.zero);
            SetScale(1f);
        }
    }
}
