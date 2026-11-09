#nullable enable
namespace TenGo.Core
{
    /// Valeurs analytics des emplacements (paramètre `placement`, identique à iOS).
    public static class RewardedPlacements
    {
        public static string Id(this RewardedPlacement placement) => placement switch
        {
            RewardedPlacement.MenuCoins => "menu_coins",
            RewardedPlacement.Hint => "hint",
            _ => "second_chance",
        };
    }
}
