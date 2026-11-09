#nullable enable
using System.Collections.Generic;

namespace TenGo.Core
{
    /// Historique du miroir : parties mesurées et décompositions découvertes
    /// (clé → date de découverte en ms Unix). Valeur immuable.
    public sealed class MirrorData
    {
        public static readonly MirrorData Empty = new MirrorData(new List<MirrorRecord>(), new Dictionary<string, long>());

        public MirrorData(IReadOnlyList<MirrorRecord> records, IReadOnlyDictionary<string, long> discovered)
        {
            Records = records;
            Discovered = discovered;
        }

        public IReadOnlyList<MirrorRecord> Records { get; }
        public IReadOnlyDictionary<string, long> Discovered { get; }
    }
}
