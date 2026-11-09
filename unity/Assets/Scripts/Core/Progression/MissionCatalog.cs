#nullable enable
using System.Collections.Generic;
using System.Linq;

namespace TenGo.Core
{
    /// Catalogue des missions, verbatim de MissionCatalog (MissionManager.swift).
    /// L'ordre compte : il est mélangé par le tirage déterministe du jour.
    public static class MissionCatalog
    {
        public const int MissionsPerDay = 3;

        public static readonly IReadOnlyList<MissionDefinition> Regular = new[]
        {
            new MissionDefinition("chain5x2", MissionKind.ChainAtLeast, 5, 2, 12, false, "mission.chain_at_least"),
            new MissionDefinition("chain7x1", MissionKind.ChainAtLeast, 7, 1, 12, false, "mission.chain_at_least"),
            new MissionDefinition("score3000", MissionKind.CumulativeScore, 0, 3000, 10, false, "mission.cumulative_score"),
            new MissionDefinition("score6000", MissionKind.CumulativeScore, 0, 6000, 18, false, "mission.cumulative_score"),
            new MissionDefinition("games2", MissionKind.GamesPlayed, 0, 2, 10, false, "mission.games_played"),
            new MissionDefinition("games3", MissionKind.GamesPlayed, 0, 3, 15, false, "mission.games_played"),
            new MissionDefinition("perfect1", MissionKind.PerfectBoards, 0, 1, 20, false, "mission.perfect_boards"),
            new MissionDefinition("moves20", MissionKind.MovesPlayed, 0, 20, 12, false, "mission.moves_played"),
            new MissionDefinition("moves35", MissionKind.MovesPlayed, 0, 35, 18, false, "mission.moves_played"),
        };

        public static readonly IReadOnlyList<MissionDefinition> SuperMissions = new[]
        {
            new MissionDefinition("chain15x1", MissionKind.ChainAtLeast, 15, 1, 45, true, "mission.chain_at_least"),
            new MissionDefinition("score15000", MissionKind.CumulativeScore, 0, 15000, 45, true, "mission.cumulative_score"),
        };

        public static MissionDefinition? Definition(string id) =>
            Regular.FirstOrDefault(d => d.Id == id) ?? SuperMissions.FirstOrDefault(d => d.Id == id);
    }
}
