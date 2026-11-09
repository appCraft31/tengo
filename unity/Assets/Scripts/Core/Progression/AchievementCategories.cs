#nullable enable
namespace TenGo.Core
{
    /// Clés de texte et icônes des catégories de succès.
    public static class AchievementCategories
    {
        /// `achievement.category.chain`…
        public static string TitleKey(this AchievementCategory c) => "achievement.category." + c.Id();

        /// `rawValue` Swift.
        public static string Id(this AchievementCategory c) => c switch
        {
            AchievementCategory.Chain => "chain",
            AchievementCategory.Perfect => "perfect",
            AchievementCategory.Score => "score",
            _ => "daily",
        };

        /// Nom du symbole SF Symbols d'iOS (à remplacer par une icône vectorielle).
        public static string IconName(this AchievementCategory c) => c switch
        {
            AchievementCategory.Chain => "link",
            AchievementCategory.Perfect => "sparkles",
            AchievementCategory.Score => "star.fill",
            _ => "flame.fill",
        };
    }
}
