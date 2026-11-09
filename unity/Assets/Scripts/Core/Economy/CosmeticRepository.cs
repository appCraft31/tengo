#nullable enable
using System.Collections.Generic;
using System.Linq;

namespace TenGo.Core
{
    /// Possession et sélection des cosmétiques (CosmeticManager.swift).
    /// « classic » toujours possédé ; achat en pièces.
    public sealed class CosmeticRepository
    {
        private readonly ISaveStore _store;
        private readonly EconomyRepository _economy;

        public CosmeticRepository(ISaveStore store, EconomyRepository economy)
        {
            _store = store;
            _economy = economy;
        }

        private static string OwnedKey(CosmeticKind kind) =>
            kind == CosmeticKind.BubbleStyle ? SaveKeys.OwnedBubbleStyles : SaveKeys.OwnedTrails;

        private static string ActiveKey(CosmeticKind kind) =>
            kind == CosmeticKind.BubbleStyle ? SaveKeys.ActiveBubbleStyle : SaveKeys.ActiveTrail;

        private IReadOnlyList<string> StoredOwned(CosmeticKind kind) =>
            _store.GetStringList(OwnedKey(kind)) ?? new List<string>();

        public bool Owns(CosmeticKind kind, string id) => id == CosmeticCatalog.DefaultId || StoredOwned(kind).Contains(id);

        /// Achète un style (débite les pièces). Vrai s'il est déjà possédé.
        public bool Purchase(CosmeticStyle style)
        {
            if (Owns(style.Kind, style.Id)) return true;
            if (!_economy.SpendCoins(style.Price)) return false;
            var owned = new HashSet<string>(StoredOwned(style.Kind)) { style.Id };
            _store.SetStringList(OwnedKey(style.Kind), owned.ToList());
            return true;
        }

        public string ActiveId(CosmeticKind kind) => _store.GetString(ActiveKey(kind)) ?? CosmeticCatalog.DefaultId;

        public void SetActive(CosmeticKind kind, string id)
        {
            if (!Owns(kind, id)) return;
            _store.SetString(ActiveKey(kind), id);
        }

        public CosmeticStyle ActiveBubbleStyle => CosmeticCatalog.ById(CosmeticKind.BubbleStyle, ActiveId(CosmeticKind.BubbleStyle));

        public CosmeticStyle ActiveTrail => CosmeticCatalog.ById(CosmeticKind.Trail, ActiveId(CosmeticKind.Trail));
    }
}
