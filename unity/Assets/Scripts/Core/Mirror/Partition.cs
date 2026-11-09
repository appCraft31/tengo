#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace TenGo.Core
{
    /// Décompositions de 10 (CognitiveMirror.swift, `Partition`).
    public static class Partition
    {
        private static readonly Lazy<IReadOnlyList<IReadOnlyList<int>>> AllLazy =
            new Lazy<IReadOnlyList<IReadOnlyList<int>>>(Build);

        /// Clé canonique : valeurs triées jointes par « + » (« 1+2+7 »).
        public static string Key(IEnumerable<int> values) => string.Join("+", values.OrderBy(v => v));

        /// Les 41 façons d'écrire 10 avec au moins deux nombres de 1 à 9,
        /// rangées par nombre de termes puis dans l'ordre lexicographique.
        public static IReadOnlyList<IReadOnlyList<int>> All => AllLazy.Value;

        public static IReadOnlyList<string> AllKeys => All.Select(p => Key(p)).ToList();

        public static int Total => All.Count;

        /// Texte affiché : « 2 + 3 + 5 ».
        public static string Display(string key) => key.Replace("+", " + ");

        private static IReadOnlyList<IReadOnlyList<int>> Build()
        {
            var result = new List<List<int>>();

            void Recurse(int remaining, int maxPart, List<int> current)
            {
                if (remaining == 0)
                {
                    if (current.Count >= 2) result.Add(current.OrderBy(v => v).ToList());
                    return;
                }
                for (int part = Math.Min(Math.Min(maxPart, remaining), 9); part >= 1; part--)
                {
                    current.Add(part);
                    Recurse(remaining - part, part, current);
                    current.RemoveAt(current.Count - 1);
                }
            }

            Recurse(10, 9, new List<int>());
            result.Sort((a, b) =>
            {
                if (a.Count != b.Count) return a.Count.CompareTo(b.Count);
                for (int i = 0; i < a.Count; i++)
                    if (a[i] != b[i]) return a[i].CompareTo(b[i]);
                return 0;
            });
            return result.Select(l => (IReadOnlyList<int>)l).ToList();
        }
    }
}
