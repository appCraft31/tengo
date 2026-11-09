#nullable enable
using System.Collections.Generic;
using System.Linq;

namespace TenGo.Core
{
    /// Possession et sélection des thèmes (ThemeManager.swift). Le thème
    /// « default » est gratuit et toujours possédé ; achat en pièces.
    public sealed class ThemeRepository
    {
        private readonly ISaveStore _store;
        private readonly EconomyRepository _economy;

        public ThemeRepository(ISaveStore store, EconomyRepository economy)
        {
            _store = store;
            _economy = economy;
        }

        /// Identifiants possédés (défaut inclus d'office).
        public IReadOnlyCollection<string> OwnedIds
        {
            get
            {
                var owned = new HashSet<string>(_store.GetStringList(SaveKeys.OwnedThemes) ?? new List<string>());
                owned.Add(Theme.DefaultId);
                return owned;
            }
        }

        public bool Owns(string id) => OwnedIds.Contains(id);

        /// Achète un thème (débite les pièces). Vrai s'il est déjà possédé ; faux
        /// si les fonds manquent.
        public bool Purchase(Theme theme)
        {
            if (Owns(theme.Id)) return true;
            if (!_economy.SpendCoins(theme.Price)) return false;
            var owned = OwnedIds.ToList();
            owned.Add(theme.Id);
            _store.SetStringList(SaveKeys.OwnedThemes, owned);
            return true;
        }

        public string ActiveId => _store.GetString(SaveKeys.ActiveTheme) ?? Theme.DefaultId;

        public void SetActive(string id)
        {
            if (!Owns(id)) return;
            _store.SetString(SaveKeys.ActiveTheme, id);
        }

        /// Thème actif (défaut si l'identifiant stocké est inconnu).
        public Theme Active => ThemeCatalog.ById(ActiveId) ?? ThemeCatalog.Default;
    }
}
