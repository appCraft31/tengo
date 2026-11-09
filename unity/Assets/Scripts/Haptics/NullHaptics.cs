using System.Collections.Generic;

namespace TenGo.Haptics
{
    /// Sans vibration (éditeur, appareils sans moteur). Garde la trace des
    /// appels pour les tests et la vitrine.
    public sealed class NullHaptics : IHaptics
    {
        public readonly List<(HapticStyle style, float intensity)> Impacts = new();
        public readonly List<HapticNotification> Notifications = new();

        public void Prepare() { }
        public void Impact(HapticStyle style, float intensity) => Impacts.Add((style, intensity));
        public void Notify(HapticNotification type) => Notifications.Add(type);
    }
}
