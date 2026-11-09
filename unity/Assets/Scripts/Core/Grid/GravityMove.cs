#nullable enable
namespace TenGo.Core
{
    /// Déplacement d'une bulle par la gravité (pilote l'animation de chute).
    public readonly struct GravityMove
    {
        public GravityMove(BubbleModel bubble, int fromRow, int toRow, int col)
        {
            Bubble = bubble;
            FromRow = fromRow;
            ToRow = toRow;
            Col = col;
        }

        public BubbleModel Bubble { get; }
        public int FromRow { get; }
        public int ToRow { get; }
        public int Col { get; }
    }
}
