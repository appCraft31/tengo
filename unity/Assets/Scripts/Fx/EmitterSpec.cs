using UnityEngine;

namespace TenGo.Fx
{
    /// Réglages d'un émetteur, champ pour champ comme `SKEmitterNode`. Les
    /// plages (`…Range`) sont la **largeur totale** : valeur tirée dans
    /// [v − range/2 ; v + range/2]. Vitesses, accélérations et tailles en points.
    [System.Serializable]
    public struct EmitterSpec
    {
        public float BirthRate;          // particules par seconde
        public int NumToEmit;            // 0 = sans limite
        public float RemoveAfter;        // émetteur retiré après ce temps (< 0 : jamais)
        public float Lifetime, LifetimeRange;
        public float Speed, SpeedRange;
        public float EmissionAngle, EmissionAngleRange;   // radians
        public float YAcceleration;
        public float Alpha, AlphaRange, AlphaSpeed;
        public float Scale, ScaleRange, ScaleSpeed;
        public Color Color;                               // sRGB
        public float ColorBlendFactor, ColorBlendFactorRange;
        public Vector2 PositionRangePt;
        public float BaseSizePt;                          // côté de la texture à l'échelle 1
        public bool Additive;

        /// Confettis de WinCelebration.makeConfetti : 180/s, 220 au plus, 2,2 ± 0,4 s,
        /// vers le bas (−90° ± 15°) à 260 ± 60 pt/s, gravité −320, échelle
        /// 0,32 ± 0,09 (−0,06/s) de la texture `dot` de 32 pt, alpha 0,95
        /// (−0,35/s), teinte d'accent à 80-100 % (blend 1 ± 0,2, borné), mélange normal.
        public static EmitterSpec Confetti(Color accent, float sceneWidthPt) => new()
        {
            BirthRate = 180, NumToEmit = 220, RemoveAfter = 3.0f,
            Lifetime = 2.2f, LifetimeRange = 0.8f,
            Speed = 260, SpeedRange = 120,
            EmissionAngle = -Mathf.PI / 2f, EmissionAngleRange = Mathf.PI / 6f,
            YAcceleration = -320,
            Alpha = 0.95f, AlphaRange = 0, AlphaSpeed = -0.35f,
            Scale = 0.32f, ScaleRange = 0.18f, ScaleSpeed = -0.06f,
            Color = accent, ColorBlendFactor = 1f, ColorBlendFactorRange = 0.4f,
            PositionRangePt = new Vector2(sceneWidthPt, 10),
            BaseSizePt = 32f, Additive = false,
        };
    }
}
