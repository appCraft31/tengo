#nullable enable
using System.Collections.Generic;
using System.Linq;

namespace TenGo.Core
{
    /// Codes des duels joués, du plus récent au plus ancien, 20 au plus
    /// (DuelHistory, DuelService.swift). Local par nécessité : les règles
    /// Firestore interdisent d'énumérer la collection `duels`.
    public sealed class DuelHistoryRepository
    {
        public const int Maximum = 20;

        private readonly ISaveStore _store;

        public DuelHistoryRepository(ISaveStore store)
        {
            _store = store;
        }

        public IReadOnlyList<string> Codes => _store.GetStringList(SaveKeys.DuelHistory) ?? new List<string>();

        /// Place le code en tête (sans doublon), tronque à 20.
        public void Remember(string code)
        {
            if (string.IsNullOrEmpty(code)) return;
            var list = Codes.Where(c => c != code).ToList();
            list.Insert(0, code);
            _store.SetStringList(SaveKeys.DuelHistory, list.Take(Maximum));
        }
    }
}
