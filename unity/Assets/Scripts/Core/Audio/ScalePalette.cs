#nullable enable
namespace TenGo.Core
{
    /// Tonalité + gamme d'une partie (son 4.0) : racine exprimée en demi-tons
    /// autour de La 220 Hz, pentatonique majeure ou mineure.
    public readonly struct ScalePalette
    {
        /// Nom lisible (journal, rendu de contrôle).
        public string Name { get; }
        /// Décalage de la racine en demi-tons par rapport à La 220 Hz.
        public int RootSemitones { get; }
        /// Pentatonique mineure (sinon majeure).
        public bool Minor { get; }

        public ScalePalette(string name, int rootSemitones, bool minor)
        {
            Name = name;
            RootSemitones = rootSemitones;
            Minor = minor;
        }

        /// Fréquence de la racine, en Hz.
        public double Root => Scales.A3 * System.Math.Pow(2.0, RootSemitones / 12.0);

        public override string ToString() => Name;
    }
}
