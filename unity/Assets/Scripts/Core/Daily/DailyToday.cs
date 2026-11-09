#nullable enable
namespace TenGo.Core
{
    /// Défi du jour prêt à jouer : grille (copie propre à l'appelant), twist,
    /// clé du jour et difficulté mesurée (1–100).
    public sealed class DailyToday
    {
        public DailyToday(GridModel grid, DailyTwist twist, int dayKey, int difficulty)
        {
            Grid = grid;
            Twist = twist;
            DayKey = dayKey;
            Difficulty = difficulty;
        }

        public GridModel Grid { get; }
        public DailyTwist Twist { get; }
        public int DayKey { get; }
        public int Difficulty { get; }
    }
}
