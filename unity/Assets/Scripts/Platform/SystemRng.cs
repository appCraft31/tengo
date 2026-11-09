using System;
using TenGo.Core;

namespace TenGo.Platform
{
    /// Aléa « système » (`SystemRandomNumberGenerator` de Swift) : grilles du
    /// mode normal et du Rush, mélanges, tirages des missions. Un SplitMix64
    /// semé au hasard suffit : ces grilles n'ont pas à être reproductibles.
    public static class SystemRng
    {
        static readonly Random Seeder = new();

        public static IRng Create()
        {
            var bytes = new byte[8];
            lock (Seeder) Seeder.NextBytes(bytes);
            ulong seed = BitConverter.ToUInt64(bytes, 0) ^ (ulong)DateTime.UtcNow.Ticks;
            return new SeededGenerator(seed);
        }
    }
}
