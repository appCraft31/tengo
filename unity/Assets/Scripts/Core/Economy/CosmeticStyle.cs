#nullable enable
namespace TenGo.Core
{
    /// Cosmétique achetable (matière de bulle ou style de tracé).
    public sealed class CosmeticStyle
    {
        public CosmeticStyle(CosmeticKind kind, string id, int price)
        {
            Kind = kind;
            Id = id;
            Price = price;
        }

        public CosmeticKind Kind { get; }
        public string Id { get; }
        public int Price { get; }

        /// Clé de texte : `style.<id>` ou `trail.<id>`.
        public string NameKey => (Kind == CosmeticKind.BubbleStyle ? "style." : "trail.") + Id;
    }
}
