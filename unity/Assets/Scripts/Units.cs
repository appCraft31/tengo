using UnityEngine;

namespace TenGo
{
    /// Conversion entre les unités logiques du jeu iOS (unités de scène SpriteKit,
    /// surface 750 × 1334, origine en bas à gauche) et les unités
    /// Unity (1 unité = 100 unités de scène, origine au centre de l'écran). Garder des
    /// objets de l'ordre de l'unité convient à Physics2D (Box2D).
    public static class Units
    {
        public const float PointsPerUnit = 100f;

        public static float FromPoints(float points) => points / PointsPerUnit;

        /// Point SpriteKit relatif au parent (x, y en points) → position locale Unity.
        public static Vector2 P(float x, float y) => new Vector2(x / PointsPerUnit, y / PointsPerUnit);

        /// Taille en points → unités.
        public static Vector2 Size(float w, float h) => new Vector2(w / PointsPerUnit, h / PointsPerUnit);
        public static float ToPoints(float units) => units * PointsPerUnit;

        /// Point de l'écran de référence (origine en bas à gauche) → monde Unity.
        public static Vector2 ScreenPointToWorld(float x, float y) =>
            new Vector2((x - (float)Core.ScreenRef.Width / 2f) / PointsPerUnit,
                        (y - (float)Core.ScreenRef.Height / 2f) / PointsPerUnit);

        /// Demi-hauteur de la caméra orthographique couvrant l'écran de référence.
        public static float ReferenceOrthoSize => (float)Core.ScreenRef.Height / 2f / PointsPerUnit;
    }
}
