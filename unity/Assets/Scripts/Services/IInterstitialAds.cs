using System;

namespace TenGo.Services
{
    /// Réseau d'interstitielles (AdMob : `AdMobInterstitialAds`). La cadence ne se décide pas
    /// ici : `InterstitialAdManager` applique `InterstitialPolicy` de Core.
    public interface IInterstitialAds
    {
        /// Une annonce est chargée et peut s'afficher tout de suite.
        bool IsReady { get; }
        /// Demande une annonce si aucune n'est prête ni en cours (chargement
        /// paresseux d'iOS : après une partie terminée, une fois le SDK démarré).
        void Load();
        /// Présente l'annonce ; `onPresented` à l'ouverture, `onClosed(vue)` à
        /// la fermeture (faux si elle n'a pas pu s'afficher).
        void Show(Action onPresented, Action<bool> onClosed);
    }
}
