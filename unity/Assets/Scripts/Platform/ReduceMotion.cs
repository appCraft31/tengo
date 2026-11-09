using System.Runtime.InteropServices;
using UnityEngine;

namespace TenGo.Platform
{
    /// Réglage d'accessibilité « Réduire les animations » du système :
    /// iOS `UIAccessibility.isReduceMotionEnabled` (greffon TenGoHaptics.mm),
    /// Android « Supprimer les animations » (`Settings.Global.ANIMATOR_DURATION_SCALE` = 0).
    /// Faux en éditeur.
    public static class ReduceMotion
    {
#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] static extern bool _TenGoReduceMotionEnabled();
#endif

        public static bool IsEnabled()
        {
#if UNITY_IOS && !UNITY_EDITOR
            try { return _TenGoReduceMotionEnabled(); } catch { return false; }
#elif UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                using var resolver = activity.Call<AndroidJavaObject>("getContentResolver");
                using var global = new AndroidJavaClass("android.provider.Settings$Global");
                return global.CallStatic<float>("getFloat", resolver, "animator_duration_scale", 1f) == 0f;
            }
            catch { return false; }
#else
            return false;
#endif
        }
    }
}
