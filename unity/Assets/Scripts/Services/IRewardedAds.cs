using System;
using TenGo.Core;

namespace TenGo.Services
{
    /// Vidéos récompensées (RewardedAdManager.swift ; AdMob à l'étape 7).
    /// Le quota se décide dans `RewardedPolicy` (Core).
    public interface IRewardedAds
    {
        /// Une vidéo est chargée.
        bool IsReady { get; }
        /// Précharge si besoin (`preloadIfNeeded`).
        void Preload();
        /// Montre la vidéo : `onReward` quand la récompense est acquise,
        /// `onDismiss(récompensé)` à la fermeture, `onUnavailable` si rien n'a
        /// pu s'afficher.
        void Show(RewardedPlacement placement, Action onReward, Action<bool> onDismiss, Action onUnavailable);
    }
}
