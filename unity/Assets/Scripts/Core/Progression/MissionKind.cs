#nullable enable
namespace TenGo.Core
{
    /// Ce que compte une mission (MissionManager.swift).
    public enum MissionKind
    {
        /// Chaîne d'au moins `Param` bulles, `Target` fois dans la journée.
        ChainAtLeast,
        /// Score cumulé sur la journée, toutes parties confondues.
        CumulativeScore,
        /// Parties terminées dans la journée.
        GamesPlayed,
        /// Grilles entièrement vidées dans la journée.
        PerfectBoards,
        /// Chaînes validées dans la journée.
        MovesPlayed,
    }
}
