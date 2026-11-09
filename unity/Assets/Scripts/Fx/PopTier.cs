namespace TenGo.Fx
{
    /// Palier d'intensité d'une validation, dérivé de la longueur de la chaîne
    /// (JuiceDirector.swift) : 2-3 bulles, 4-5, 6 et plus.
    public enum PopTier { Small, Medium, Large }

    public static class PopTiers
    {
        public static PopTier ForLength(int length) => length <= 3 ? PopTier.Small : length <= 5 ? PopTier.Medium : PopTier.Large;

        /// Monte d'un cran (record de chaîne battu dans la partie).
        public static PopTier Boosted(this PopTier t) => t == PopTier.Small ? PopTier.Medium : PopTier.Large;

        /// Éclats par bulle : le palier « small » reste plus riche que l'ancien rendu uniforme.
        public static int BurstCount(this PopTier t) => t switch { PopTier.Small => 16, PopTier.Medium => 24, _ => 34 };
        public static int RingCount(this PopTier t) => t switch { PopTier.Small => 0, PopTier.Medium => 1, _ => 2 };
        /// Amplitude de secousse, en points.
        public static float ShakeAmplitude(this PopTier t) => t switch { PopTier.Small => 0, PopTier.Medium => 4, _ => 9 };
        public static float ShakeDuration(this PopTier t) => t switch { PopTier.Small => 0, PopTier.Medium => 0.14f, _ => 0.24f };
        /// Zoom avant bref de la caméra, dès le palier « small ».
        public static float CameraPunch(this PopTier t) => t switch { PopTier.Small => 0.995f, PopTier.Medium => 0.986f, _ => 0.974f };
        /// Arrêt sur image (seul effet qui touche au temps du jeu), chaînes de 4 et plus.
        public static float HitStop(this PopTier t) => t switch { PopTier.Small => 0, PopTier.Medium => 0.035f, _ => 0.060f };
        public static float FlashAlpha(this PopTier t) => t == PopTier.Large ? 0.18f : 0f;
        /// Poussée appliquée aux voisines de la chaîne, en points.
        public static float NeighborPush(this PopTier t) => t switch { PopTier.Small => 0, PopTier.Medium => 6, _ => 11 };
        /// Portée des éclats, en points.
        public static (float min, float max) SparkReach(this PopTier t) => t switch
        {
            PopTier.Small => (30, 66), PopTier.Medium => (38, 84), _ => (46, 108),
        };
    }
}
