#nullable enable
namespace TenGo.Core
{
    /// Palier de série : récompense unique au jour `Day`.
    public sealed class StreakMilestone
    {
        public StreakMilestone(int day, int coins, Booster? booster, int boosterQty)
        {
            Day = day;
            Coins = coins;
            Booster = booster;
            BoosterQty = boosterQty;
        }

        public int Day { get; }
        public int Coins { get; }
        public Booster? Booster { get; }
        public int BoosterQty { get; }
    }
}
