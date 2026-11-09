#nullable enable
namespace TenGo.Core
{
    /// Difficulté visée par le défi du jour (valeur 1–100), en rotation sur la
    /// semaine : 4 jours faciles ou moyens pour 3 durs (GDD §61).
    public enum DailyTarget
    {
        Easy = 30,
        Medium = 55,
        Hard = 78,
    }
}
