#nullable enable
using System;
using System.Collections.Generic;

namespace TenGo.Core
{
    /// Réductions de la bibliothèque standard Swift, reproduites au bit près :
    /// une même graine donne la même grille sur iOS, Android et Unity. Ne pas
    /// « simplifier » ces fonctions (un modulo, par exemple, casserait la parité
    /// sans qu'aucun test de plage ne le voie).
    public static class Rng
    {
        /// `RandomNumberGenerator.next(upperBound:)` de Swift : méthode « presque
        /// sans division » de Lemire, avec rejet. Entier uniforme dans `[0, bound)`.
        public static ulong NextUpperBound(this IRng rng, ulong bound)
        {
            if (bound == 0) throw new ArgumentException("upperBound cannot be zero.", nameof(bound));
            ulong random = rng.NextULong();
            ulong high = Mul128.Multiply(random, bound, out ulong low);
            if (low < bound)
            {
                ulong t = unchecked(0UL - bound) % bound;
                while (low < t)
                {
                    random = rng.NextULong();
                    high = Mul128.Multiply(random, bound, out low);
                }
            }
            return high;
        }

        /// `Int.random(in: min...max, using:)` de Swift (bornes incluses).
        public static int NextInt(this IRng rng, int min, int max)
        {
            if (min > max) throw new ArgumentException("Plage vide.");
            ulong delta = unchecked((ulong)((long)max - (long)min));
            return (int)((long)min + (long)rng.NextUpperBound(delta + 1UL));
        }

        /// `Int.random(in: 0..<bound, using:)` de Swift.
        public static int NextIntBelow(this IRng rng, int bound)
        {
            if (bound <= 0) throw new ArgumentException("Borne nulle ou négative.", nameof(bound));
            return (int)rng.NextUpperBound((ulong)bound);
        }

        /// `Array.shuffled(using:)` de Swift : Fisher-Yates DIRECT (du début
        /// vers la fin), et non l'algorithme inverse de .NET.
        public static List<T> ShuffledSwift<T>(this IReadOnlyList<T> source, IRng rng)
        {
            var a = new List<T>(source);
            if (a.Count <= 1) return a;
            int amount = a.Count;
            int current = 0;
            while (amount > 1)
            {
                int r = rng.NextIntBelow(amount);
                amount -= 1;
                int j = current + r;
                (a[current], a[j]) = (a[j], a[current]);
                current += 1;
            }
            return a;
        }

        /// `Collection.randomElement(using:)` de Swift. Faux si la liste est
        /// vide (aucun tirage n'est alors consommé, comme en Swift).
        public static bool TryRandomElementSwift<T>(this IReadOnlyList<T> source, IRng rng, out T value)
        {
            if (source.Count == 0)
            {
                value = default!;
                return false;
            }
            value = source[rng.NextIntBelow(source.Count)];
            return true;
        }

        /// `randomElement(using:)!` : la liste ne doit pas être vide.
        public static T RandomElementSwift<T>(this IReadOnlyList<T> source, IRng rng)
        {
            if (!source.TryRandomElementSwift(rng, out T value))
                throw new InvalidOperationException("Liste vide.");
            return value;
        }
    }
}
