#nullable enable
namespace TenGo.Core
{
    /// Générateur pseudo-aléatoire déterministe (SplitMix64), portage de
    /// SeededGenerator.swift. Une même graine produit toujours la même suite :
    /// base du défi du jour et des duels entre plateformes.
    public sealed class SeededGenerator : IRng
    {
        private ulong _state;

        public SeededGenerator(ulong seed)
        {
            _state = seed;
        }

        /// Graine signée (clé du jour, graine de duel) réinterprétée bit à bit,
        /// comme `UInt64(bitPattern: Int64(x))` en Swift.
        public static SeededGenerator FromSigned(long seed) => new SeededGenerator(unchecked((ulong)seed));

        public ulong NextULong()
        {
            unchecked
            {
                _state += 0x9E3779B97F4A7C15UL;
                ulong z = _state;
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                return z ^ (z >> 31);
            }
        }
    }
}
