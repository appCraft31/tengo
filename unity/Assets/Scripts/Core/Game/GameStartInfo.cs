#nullable enable
using System.Collections.Generic;

namespace TenGo.Core
{
    /// Effets du lancement d'une partie : bouclier consommé, paliers de série payés.
    public sealed class GameStartInfo
    {
        public static readonly GameStartInfo Nothing = new GameStartInfo(false, new List<StreakMilestone>());

        public GameStartInfo(bool streakProtected, IReadOnlyList<StreakMilestone> milestones)
        {
            StreakProtected = streakProtected;
            Milestones = milestones;
        }

        /// Un bouclier vient d'absorber un jour manqué.
        public bool StreakProtected { get; }
        public IReadOnlyList<StreakMilestone> Milestones { get; }
    }
}
