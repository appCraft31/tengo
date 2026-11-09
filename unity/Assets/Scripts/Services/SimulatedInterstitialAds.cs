using System;
using TenGo.Fx;
using UnityEngine;

namespace TenGo.Services
{
    /// Interstitielle simulée (`QA_FAKE_INTERSTITIAL` d'iOS) : toujours prête,
    /// « affichée » pendant `Duration` secondes réelles, sans rien dessiner.
    public sealed class SimulatedInterstitialAds : IInterstitialAds
    {
        public bool Ready = true;
        public float Duration = 0.2f;
        public int Shown { get; private set; }

        public bool IsReady => Ready;

        public void Show(Action onPresented, Action<bool> onClosed)
        {
            if (!Ready) { onClosed?.Invoke(false); return; }
            Shown++;
            Debug.Log("[Ads] interstitielle simulée");
            onPresented?.Invoke();
            FxTime.AfterRealtime(Duration, () => onClosed?.Invoke(true));
        }
    }
}
