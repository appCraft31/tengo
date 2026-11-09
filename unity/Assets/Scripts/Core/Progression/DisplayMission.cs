#nullable enable
namespace TenGo.Core
{
    /// Mission du jour prête à afficher.
    public sealed class DisplayMission
    {
        public DisplayMission(MissionDefinition definition, int progress, bool claimed)
        {
            Definition = definition;
            Progress = progress;
            Claimed = claimed;
        }

        public MissionDefinition Definition { get; }
        public int Progress { get; }
        public bool Claimed { get; }
        public bool IsCompleted => Progress >= Definition.Target;
    }
}
