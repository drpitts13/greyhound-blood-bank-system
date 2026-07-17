using BloodBankLIS.Domain.Common;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Entities;

/// <summary>
/// An authorized override of a Warning-severity rule (or an emergency release).
/// HardStops are never overridable. Append-only; the electronic-signature link is
/// added in the Security phase (see docs/safety-rules.md section 5).
/// </summary>
public class Override : BaseEntity
{
    public OverrideAction Action { get; set; } = OverrideAction.WarningOverride;

    public string ContextType { get; set; } = string.Empty;

    public long ContextId { get; set; }

    /// <summary>The specific rule code that was overridden (e.g. ISS-XM-REQUIRED).</summary>
    public string RuleCode { get; set; } = string.Empty;

    public string Reason { get; set; } = string.Empty;

    public string AuthorizedBy { get; set; } = string.Empty;

    public long? SignatureId { get; set; }

    public DateTime OverriddenUtc { get; set; }

    /// <summary>
    /// Optional structured resolution for result-context overrides (e.g. Retain vs Replace
    /// for RES-ABORH-DELTA). Null for issue-path overrides.
    /// </summary>
    public string? Resolution { get; set; }
}
