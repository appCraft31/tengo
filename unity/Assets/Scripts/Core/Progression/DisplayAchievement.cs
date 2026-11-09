#nullable enable
namespace TenGo.Core
{
    /// Succès prêt à afficher. Comme sur iOS, « débloqué » se déduit de la
    /// statistique (valeur ≥ cible), pas de la liste des succès crédités.
    public sealed class DisplayAchievement
    {
        public DisplayAchievement(AchievementDefinition definition, int currentValue)
        {
            Definition = definition;
            CurrentValue = currentValue;
        }

        public AchievementDefinition Definition { get; }
        public int CurrentValue { get; }
        public bool IsUnlocked => CurrentValue >= Definition.Target;
    }
}
