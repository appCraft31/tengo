#nullable enable
using System.Collections.Generic;
using System.Linq;

namespace TenGo.Core
{
    /// Partie en cours et meilleurs scores locaux (GameState.swift) : top 10
    /// du mode Zen et, séparément, du mode Rush (monnaie de score différente).
    public sealed class GameStateRepository
    {
        public const int MaxScores = 10;

        private readonly ISaveStore _store;

        public GameStateRepository(ISaveStore store)
        {
            _store = store;
        }

        // MARK: - Partie en cours

        public void Save(GridModel grid, int score) => _store.SetString(SaveKeys.SavedGame, new SavedGame(grid, score).ToJson());

        public SavedGame? Load()
        {
            string? json = _store.GetString(SaveKeys.SavedGame);
            return json == null ? null : SavedGame.FromJson(json);
        }

        public void Clear() => _store.Remove(SaveKeys.SavedGame);

        public bool Exists => _store.Has(SaveKeys.SavedGame);

        // MARK: - Meilleurs scores (Zen)

        public IReadOnlyList<int> HighScores => _store.GetIntList(SaveKeys.HighScores) ?? new List<int>();

        public void AddScore(int score) => Insert(SaveKeys.HighScores, HighScores, score);

        /// Partie continuée : son premier score est remplacé par le score final.
        public void ReplaceScore(int previous, int score)
        {
            var scores = HighScores.ToList();
            int i = scores.IndexOf(previous);
            if (i >= 0) scores.RemoveAt(i);
            Insert(SaveKeys.HighScores, scores, score);
        }

        public int BestScore => HighScores.Count > 0 ? HighScores[0] : 0;

        // MARK: - Rush

        public IReadOnlyList<int> RushHighScores => _store.GetIntList(SaveKeys.RushHighScores) ?? new List<int>();

        public void AddRushScore(int score) => Insert(SaveKeys.RushHighScores, RushHighScores, score);

        public int RushBest => RushHighScores.Count > 0 ? RushHighScores[0] : 0;

        private void Insert(string key, IEnumerable<int> current, int score)
        {
            var scores = current.ToList();
            scores.Add(score);
            _store.SetIntList(key, scores.OrderByDescending(s => s).Take(MaxScores));
        }
    }
}
