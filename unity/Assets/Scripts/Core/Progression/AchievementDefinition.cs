#nullable enable
namespace TenGo.Core
{
    /// Définition d'un succès. `TitleKey` et `DescKey` se formatent avec `%d` = `Target`.
    public sealed class AchievementDefinition
    {
        public AchievementDefinition(string id, AchievementCategory category, int target, string titleKey, string descKey,
            int coinReward, int xpReward, AchievementStat stat)
        {
            Id = id;
            Category = category;
            Target = target;
            TitleKey = titleKey;
            DescKey = descKey;
            CoinReward = coinReward;
            XpReward = xpReward;
            Stat = stat;
        }

        public string Id { get; }
        public AchievementCategory Category { get; }
        public int Target { get; }
        public string TitleKey { get; }
        public string DescKey { get; }
        public int CoinReward { get; }
        public int XpReward { get; }
        public AchievementStat Stat { get; }
    }
}
