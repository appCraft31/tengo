#nullable enable
namespace TenGo.Core
{
    /// Surface de référence des scènes SpriteKit de tenGO (750 × 1334, portrait,
    /// mise à l'échelle aspectFill). Core raisonne dans ces unités de scène ; la
    /// couche jeu les convertit en unités Unity (voir CONVENTIONS.md).
    public static class ScreenRef
    {
        public const double Width = 750;
        public const double Height = 1334;

        /// Rapport hauteur / largeur de la surface de référence.
        public static double Aspect => Height / Width;
    }
}
