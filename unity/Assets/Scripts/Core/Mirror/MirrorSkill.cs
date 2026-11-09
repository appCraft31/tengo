#nullable enable
namespace TenGo.Core
{
    /// Les quatre aptitudes mesurées par le miroir.
    public enum MirrorSkill
    {
        /// Coup d'œil : temps pour trouver un 10 (plus court = mieux).
        Look,
        /// Vision : bulles par chaîne.
        Vision,
        /// Anticipation : chaînes qui utilisent une bulle tombée au coup précédent.
        Anticipation,
        /// Répertoire : décompositions de 10 trouvées, sur 41.
        Repertoire,
    }
}
