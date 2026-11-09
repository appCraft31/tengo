using System.Runtime.InteropServices;

namespace TenGo.Haptics
{
    /// Vibrations iOS par le greffon `Plugins/iOS/TenGoHaptics.mm`
    /// (UIImpactFeedbackGenerator, un générateur par style, et
    /// UINotificationFeedbackGenerator) : mêmes appels que HapticManager.swift.
    public sealed class IosHaptics : IHaptics
    {
#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void _TenGoHapticPrepare();
        [DllImport("__Internal")] static extern void _TenGoHapticImpact(int style, float intensity);
        [DllImport("__Internal")] static extern void _TenGoHapticNotify(int type);
#endif

        public void Prepare()
        {
#if UNITY_IOS && !UNITY_EDITOR
            _TenGoHapticPrepare();
#endif
        }

        public void Impact(HapticStyle style, float intensity)
        {
#if UNITY_IOS && !UNITY_EDITOR
            _TenGoHapticImpact((int)style, intensity);
#endif
        }

        public void Notify(HapticNotification type)
        {
#if UNITY_IOS && !UNITY_EDITOR
            _TenGoHapticNotify((int)type);
#endif
        }
    }
}
