using BloodBankLIS.Domain.Common;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Entities;

/// <summary>
/// Persisted scan-session accumulator for multi-request UI / rapid scans.
/// </summary>
public class BloodComponentScanSession : BaseEntity
{
    public Guid SessionKey { get; set; }

    public string ExpectedStructuresJson { get; set; } = "[]";

    public string ReceivedStructuresJson { get; set; } = "[]";

    public string DraftJson { get; set; } = "{}";

    public DateTime StartedAt { get; set; }

    public DateTime LastScanAt { get; set; }

    public bool IsCompleted { get; set; }

    public string StartedBy { get; set; } = "system";

    public string? CompletedComponentIdentity { get; set; }

    public ICollection<BloodComponentScanSessionLine> Lines { get; set; } = new List<BloodComponentScanSessionLine>();
}

public class BloodComponentScanSessionLine : BaseEntity
{
    public long ScanSessionId { get; set; }

    public BloodComponentScanSession? Session { get; set; }

    public IsbtDataStructureKind StructureKind { get; set; }

    public string OriginalValue { get; set; } = string.Empty;

    public string SanitizedValue { get; set; } = string.Empty;

    public bool WasDuplicate { get; set; }

    public bool WasConflict { get; set; }

    public DateTime ScannedAt { get; set; }
}
