#nullable enable
namespace TenGo.Core
{
    /// Récompense d'une mission réclamée : pièces, XP, et bouclier de série
    /// éventuel (super mission, 1 fois sur 4).
    public sealed class MissionClaimResult
    {
        public MissionClaimResult(int coins, GainResult xp, bool shieldGranted)
        {
            Coins = coins;
            Xp = xp;
            ShieldGranted = shieldGranted;
        }

        public int Coins { get; }
        public GainResult Xp { get; }
        public bool ShieldGranted { get; }
    }
}
