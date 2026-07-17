using BloodBankLIS.Domain.Common;

namespace BloodBankLIS.Domain.Entities;

/// <summary>
/// Record of an issued unit being returned to inventory. The reissue-eligibility
/// evaluation (storage/integrity/time-temp) is captured so the decision is
/// auditable (see docs/workflows.md section 7).
/// </summary>
public class Return : BaseEntity
{
    public long IssueId { get; set; }

    public Issue? Issue { get; set; }

    public long BloodProductId { get; set; }

    public DateTime ReturnedUtc { get; set; }

    public string ReturnedBy { get; set; } = "system";

    public string Reason { get; set; } = string.Empty;

    public bool ReissueEligible { get; set; }

    /// <summary>Serialized per-check evaluation behind the reissue decision.</summary>
    public string? ReissueEvaluationJson { get; set; }
}
