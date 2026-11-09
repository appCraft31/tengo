#nullable enable
namespace TenGo.Core
{
    /// Moment où une interstitielle peut être proposée (InterstitialAdManager.swift).
    public enum AdTrigger
    {
        /// Bouton ⌂ : dès la 2e partie terminée de la session.
        Home,
        /// Bouton Rejouer : dès la 2e partie.
        Replay,
        /// 1,5 s après le panneau de fin : 1 sur 4, dès la 3e partie.
        GameOverAuto,
    }
}
