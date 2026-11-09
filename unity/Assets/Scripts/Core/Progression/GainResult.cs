#nullable enable
namespace TenGo.Core
{
    /// Résultat d'un gain d'XP (écran de fin de partie). Structure : l'égalité
    /// compare tous les champs.
    public readonly struct GainResult
    {
        public GainResult(int xpGained, int previousLevel, int newLevel)
        {
            XpGained = xpGained;
            PreviousLevel = previousLevel;
            NewLevel = newLevel;
        }

        public int XpGained { get; }
        public int PreviousLevel { get; }
        public int NewLevel { get; }
        public bool LeveledUp => NewLevel > PreviousLevel;
    }
}
