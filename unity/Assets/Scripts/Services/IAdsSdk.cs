using System;

namespace TenGo.Services
{
    /// Démarrage du SDK publicitaire (`MobileAds.start`), une fois le
    /// consentement recueilli : rien n'est demandé au réseau avant.
    public interface IAdsSdk
    {
        /// `MobileAds.start` terminé (`isAdsSessionStarted`).
        bool Started { get; }
        /// Démarre le SDK dans le mode choisi ; `onReady` sur le fil principal.
        void Start(bool personalized, Action onReady);
    }

    /// SDK simulé : prêt tout de suite.
    public sealed class SimulatedAdsSdk : IAdsSdk
    {
        public bool Started { get; private set; }
        public bool Personalized { get; private set; }

        public void Start(bool personalized, Action onReady)
        {
            Started = true;
            Personalized = personalized;
            UnityEngine.Debug.Log("[AdMob] Démarrage pubs — mode " + (personalized ? "personnalisé" : "non personnalisé") + " (simulé)");
            onReady?.Invoke();
        }
    }
}
