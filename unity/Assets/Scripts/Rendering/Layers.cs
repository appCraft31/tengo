namespace TenGo.Rendering
{
    /// Ordres d'affichage : `zPosition` SpriteKit × 10 (GameScene.swift).
    public static class Layers
    {
        /// Fond animé du thème (z −1).
        public const int Background = -10;
        /// Bulles (z 0 ; chiffre z 2 ; fleur z 1).
        public const int Bubbles = 0;
        /// Conteneur du tracé (z 5) ; la comète y est à z 2.
        public const int Trail = 50;
        public const int Comet = 70;
        /// Couche d'effets (z 12).
        public const int Fx = 120;
        /// Éclats d'une bulle (z 15 dans la couche d'effets).
        public const int Burst = Fx + 150;
        /// Célébration de victoire (z 30 dans la couche d'effets).
        public const int Celebration = Fx + 300;
        /// Flash plein écran (z 40 dans la couche d'effets).
        public const int Flash = Fx + 400;
        /// Gain du chemin (+100), z 12.
        public const int PathGain = 120;
        /// Voile de transition d'écran, au-dessus de tout.
        public const int Transition = 10000;
    }
}
