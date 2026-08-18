using BloodBankLIS.Domain.Common;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Entities;

/// <summary>
/// Quality-system deviation / nonconformance (AABB Standard 7). Linked to the
/// clinical or interface record that triggered it; CAPA status is tracked here.
/// </summary>
public class Deviation : BaseEntity
{
    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public DeviationSeverity Severity { get; set; } = DeviationSeverity.Minor;

    public DeviationStatus Status { get; set; } = DeviationStatus.Open;

    public string? ContextType { get; set; }

    public long? ContextId { get; set; }

    public string? CorrectiveAction { get; set; }

    public string ReportedBy { get; set; } = "system";

    public DateTime ReportedUtc { get; set; }

    public string? ClosedBy { get; set; }

    public DateTime? ClosedUtc { get; set; }
}
