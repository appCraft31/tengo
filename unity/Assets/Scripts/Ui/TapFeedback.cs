using TenGo.Haptics;

namespace TenGo.Ui
{
    /// Retour haptique léger des boutons de menu (TapFeedback.swift, parité
    /// Android `tapFeedback()`). Les « Réclamer » ont leur propre retour, plus marqué.
    public static class TapFeedback
    {
        public static void Play() => HapticManager.Light();
    }
}
