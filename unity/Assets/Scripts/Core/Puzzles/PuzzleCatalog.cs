#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace TenGo.Core
{
    /// Catalogue des niveaux de Puzzles (PuzzleCatalog.swift + PuzzleWorld).
    /// Les données vivent dans `Resources/Puzzles/puzzles.json`, généré depuis
    /// le fichier Swift ; Core ne lit pas les ressources Unity : la couche jeu
    /// passe le texte JSON à `FromJson`.
    public sealed class PuzzleCatalog
    {
        /// Chemin de la ressource (`Resources.Load<TextAsset>`).
        public const string ResourcePath = "Puzzles/puzzles";

        private readonly List<PuzzleLevel> _levels;

        public PuzzleCatalog(IEnumerable<PuzzleLevel> levels)
        {
            _levels = levels.OrderBy(l => l.World).ThenBy(l => l.Index).ToList();
        }

        /// Tous les niveaux, par monde puis par index.
        public IReadOnlyList<PuzzleLevel> All => _levels;

        /// Mondes présents (`PuzzleWorld.all`).
        public IReadOnlyList<int> Worlds => _levels.Select(l => l.World).Distinct().ToList();

        /// Niveaux d'un monde, par index croissant.
        public IReadOnlyList<PuzzleLevel> Levels(int world) => _levels.Where(l => l.World == world).ToList();

        public PuzzleLevel? Level(int world, int index) =>
            _levels.FirstOrDefault(l => l.World == world && l.Index == index);

        /// Clé de texte du nom d'un monde (`puzzle.world1`).
        public static string NameKey(int world) => "puzzle.world" + world;

        /// Lit `{"levels":[{"world","index","layout","moves","twoStarScore","threeStarScore"}]}`.
        public static PuzzleCatalog FromJson(string json)
        {
            JObject root = JObject.Parse(json);
            JArray levels = root["levels"] as JArray ?? throw new FormatException("puzzles.json : tableau « levels » absent.");
            return new PuzzleCatalog(levels.Select(t => new PuzzleLevel(
                (int)t["index"]!,
                (int)t["world"]!,
                (string)t["layout"]!,
                (int)t["moves"]!,
                (int)t["twoStarScore"]!,
                (int)t["threeStarScore"]!)));
        }
    }
}
