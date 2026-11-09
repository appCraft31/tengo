namespace TenGo.Haptics
{
    /// Moteur de vibration natif (iOS : UIFeedbackGenerator, Android : Vibrator).
    /// Le réglage « vibrations » et les motifs vivent dans `HapticManager`.
    public interface IHaptics
    {
        /// `prepare()` : réveille le moteur avant un geste.
        void Prepare();

        /// `impactOccurred(intensity:)`, intensité 0-1.
        void Impact(HapticStyle style, float intensity);

        /// `notificationOccurred(_:)`.
        void Notify(HapticNotification type);
    }
}
