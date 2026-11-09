using System;
using TenGo.Core;
using TenGo.Fx;
using UnityEngine;

namespace TenGo.Services
{
    /// Vidéo récompensée simulée (`QA_FAKE_REWARDED` d'iOS) : prête, regardée
    /// jusqu'au bout après `Duration` secondes réelles. `Rewards = false`
    /// simule une vidéo coupée avant la fin.
    public sealed class SimulatedRewardedAds : IRewardedAds
    {
        public bool Ready = true;
        public bool Rewards = true;
        public float Duration = 0.3f;
        public int Shown { get; private set; }

        public bool IsReady => Ready;

        public void Preload() { }

        public void Show(RewardedPlacement placement, Action onReward, Action<bool> onDismiss, Action onUnavailable)
        {
            if (!Ready) { onUnavailable?.Invoke(); return; }
            Shown++;
            Debug.Log("[Ads] vidéo récompensée simulée : " + placement.Id());
            bool rewards = Rewards;
            FxTime.AfterRealtime(Duration, () =>
            {
                if (rewards) onReward?.Invoke();
                onDismiss?.Invoke(rewards);
            });
        }
    }
}
