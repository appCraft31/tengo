#nullable enable
using System.Linq;

namespace TenGo.Core
{
    /// Meilleures étoiles par niveau de puzzle (PuzzleProgress, PuzzleLevel.swift).
    /// Un niveau est déverrouillé dès que le précédent a au moins une étoile ;
    /// le premier l'est toujours.
    public sealed class PuzzleProgressRepository
    {
        private readonly ISaveStore _store;
        private readonly PuzzleCatalog _catalog;

        public PuzzleProgressRepository(ISaveStore store, PuzzleCatalog catalog)
        {
            _store = store;
            _catalog = catalog;
        }

        /// Étoiles d'un niveau (0 = jamais terminé).
        public int Stars(int world, int index) => _store.GetInt(SaveKeys.PuzzleStars(world, index));

        /// Enregistre un résultat ; les étoiles ne redescendent jamais.
        public void Record(int world, int index, int stars)
        {
            if (stars <= Stars(world, index)) return;
            _store.SetInt(SaveKeys.PuzzleStars(world, index), stars);
        }

        public bool IsUnlocked(int world, int index) => index <= 1 || Stars(world, index - 1) > 0;

        public int TotalStars(int world) => _catalog.Levels(world).Sum(l => Stars(world, l.Index));

        public int CompletedCount(int world) => _catalog.Levels(world).Count(l => Stars(world, l.Index) > 0);
    }
}
