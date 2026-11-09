#nullable enable
namespace TenGo.Core
{
    /// Niveau atteint, XP consommée dans ce niveau, et XP pour passer au suivant
    /// (0 au niveau maximum). Structure : l'égalité compare tous les champs.
    public readonly struct LevelBreakdown
    {
        public LevelBreakdown(int level, int xpIntoLevel, int xpForNext)
        {
            Level = level;
            XpIntoLevel = xpIntoLevel;
            XpForNext = xpForNext;
        }

        public int Level { get; }
        public int XpIntoLevel { get; }
        public int XpForNext { get; }

        public override string ToString() => "niveau " + Level + " (" + XpIntoLevel + "/" + XpForNext + ")";
    }
}
