#nullable enable
using System.Collections.Generic;

namespace TenGo.Core
{
    /// Produits intégrés (StoreManager.swift, AdFreeManager.swift) : mêmes
    /// identifiants sur les deux stores.
    public static class StoreCatalog
    {
        /// Mod « sans pub » (non-consommable).
        public const string NoAdsProductId = "com.tengo.noads";
        /// Pièces offertes avec le mod « sans pub ».
        public const int AdFreeBonusCoins = 500;
        /// L'offre « sans pub » de fin de partie suit une série d'au moins autant
        /// d'interstitielles (comptées d'une session à l'autre).
        public const int AdFreeOfferAfterAds = 2;
        /// Au plus autant d'offres « sans pub » par session.
        public const int AdFreeOffersPerSession = 2;

        /// Packs de pièces (consommables) → pièces créditées.
        public static readonly IReadOnlyDictionary<string, int> CoinPacks = new Dictionary<string, int>
        {
            ["com.tengo.coins.tier1"] = 500,
            ["com.tengo.coins.tier2"] = 1200,
            ["com.tengo.coins.tier3"] = 3000,
            ["com.tengo.coins.tier4"] = 6500,
        };

        /// Pièces d'un produit (0 si inconnu ou non-consommable).
        public static int CoinsFor(string productId) => CoinPacks.TryGetValue(productId, out int coins) ? coins : 0;
    }
}
