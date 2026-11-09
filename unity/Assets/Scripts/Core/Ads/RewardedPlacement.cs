#nullable enable
namespace TenGo.Core
{
    /// Où la vidéo récompensée est proposée (RewardedAdManager.swift).
    public enum RewardedPlacement
    {
        /// +10 pièces depuis le menu (plafonné par jour).
        MenuCoins,
        /// Indice gratuit en partie.
        Hint,
        /// Grille bloquée : mélange et on continue.
        SecondChance,
    }
}
