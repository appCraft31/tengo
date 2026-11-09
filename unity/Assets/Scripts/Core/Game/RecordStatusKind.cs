#nullable enable
namespace TenGo.Core
{
    /// Position du score face au record local.
    public enum RecordStatusKind
    {
        None,
        NewRecord,
        /// À moins de 5 % du record (plancher 20 points).
        NearMiss,
        /// Rappel du record.
        Best,
    }
}
