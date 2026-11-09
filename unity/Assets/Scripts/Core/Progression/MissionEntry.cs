#nullable enable
using System;

namespace TenGo.Core
{
    /// État persisté d'une mission du jour (`MissionManager.Entry`, JSON
    /// `{"id","value","claimed"}`).
    public sealed class MissionEntry : IEquatable<MissionEntry>
    {
        public MissionEntry(string id, int value, bool claimed)
        {
            Id = id;
            Value = value;
            Claimed = claimed;
        }

        public string Id { get; }
        public int Value { get; set; }
        public bool Claimed { get; set; }

        public bool Equals(MissionEntry? other) =>
            other != null && Id == other.Id && Value == other.Value && Claimed == other.Claimed;

        public override bool Equals(object? obj) => Equals(obj as MissionEntry);

        public override int GetHashCode() => Id.GetHashCode() ^ Value ^ (Claimed ? 1 : 0);

        public override string ToString() => Id + "=" + Value + (Claimed ? " (réclamée)" : "");
    }
}
