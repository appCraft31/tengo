using UnityEngine;

namespace TenGo.Haptics
{
    /// Vibrations Android (repris d'Orbis, `VibrationEffect.createOneShot`) :
    /// durée et amplitude par style, proportionnelles à l'intensité ;
    /// notification = impulsion courte et franche. Sans contrôle d'amplitude,
    /// l'amplitude par défaut du vibreur est utilisée.
    public sealed class AndroidHaptics : IHaptics
    {
        static bool _never;
#if UNITY_ANDROID && !UNITY_EDITOR
        readonly AndroidJavaObject _vibrator;
        readonly AndroidJavaClass _effect;
        readonly bool _amplitude;
#endif

        public AndroidHaptics()
        {
            // Référence à Handheld.Vibrate : Unity ajoute la permission VIBRATE au manifeste.
            if (_never) Handheld.Vibrate();
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                _vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
                _effect = new AndroidJavaClass("android.os.VibrationEffect");
                _amplitude = _vibrator != null && _vibrator.Call<bool>("hasAmplitudeControl");
            }
            catch (System.Exception e) { Debug.LogWarning("[Haptics] vibreur indisponible : " + e.Message); }
#endif
        }

        /// (durée ms, amplitude maximale) par style.
        public static (long ms, int amplitude) Profile(HapticStyle style) => style switch
        {
            HapticStyle.Soft => (8, 90),
            HapticStyle.Light => (10, 120),
            HapticStyle.Medium => (15, 190),
            HapticStyle.Rigid => (12, 230),
            _ => (20, 255),
        };

        public void Prepare() { }

        public void Impact(HapticStyle style, float intensity)
        {
            var (ms, amp) = Profile(style);
            OneShot(ms, Mathf.RoundToInt(amp * Mathf.Clamp01(intensity)));
        }

        public void Notify(HapticNotification type) => OneShot(type == HapticNotification.Success ? 14 : 22, 200);

        void OneShot(long ms, int amplitude)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (_vibrator == null || _effect == null) return;
            try
            {
                int amp = _amplitude ? Mathf.Clamp(amplitude, 1, 255) : -1;
                using var fx = _effect.CallStatic<AndroidJavaObject>("createOneShot", ms, amp);
                _vibrator.Call("vibrate", fx);
            }
            catch (System.Exception) { }
#endif
        }
    }
}
