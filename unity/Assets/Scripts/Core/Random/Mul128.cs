#nullable enable
namespace TenGo.Core
{
    /// Produit complet 64 × 64 → 128 bits. `Math.BigMul(ulong, ulong, out ulong)`
    /// et `UInt128` n'existent pas dans le profil .NET Standard 2.1 d'Unity :
    /// on découpe en moitiés de 32 bits, comme le portage Kotlin.
    public static class Mul128
    {
        /// Calcule `a × b` ; renvoie les 64 bits de poids fort, `low` reçoit
        /// les 64 bits de poids faible.
        public static ulong Multiply(ulong a, ulong b, out ulong low)
        {
            unchecked
            {
                const ulong mask = 0xFFFF_FFFFUL;
                ulong aLo = a & mask, aHi = a >> 32;
                ulong bLo = b & mask, bHi = b >> 32;
                ulong lo = aLo * bLo;
                ulong mid1 = aHi * bLo;
                ulong mid2 = aLo * bHi;
                ulong hi = aHi * bHi;
                ulong carry = (lo >> 32) + (mid1 & mask) + (mid2 & mask);
                low = a * b;
                return hi + (mid1 >> 32) + (mid2 >> 32) + (carry >> 32);
            }
        }
    }
}
