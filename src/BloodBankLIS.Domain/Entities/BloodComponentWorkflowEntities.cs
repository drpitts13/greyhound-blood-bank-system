using BloodBankLIS.Domain.Common;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Entities;

/// <summary>Persisted compatibility decision from the table-driven rules engine.</summary>
public class BloodComponentCompatibilityDecision : BaseEntity
{
    public long BloodProductId { get; set; }
    public BloodUnit? Unit { get; set; }
    public long PatientId { get; set; }
    public long? OrderId { get; set; }
    public CompatibilityOutcome Outcome { get; set; }
    public CompatibilityPathway Pathway { get; set; }
    public string SatisfiedRulesJson { get; set; } = "[]";
    public string WarningsJson { get; set; } = "[]";
    public string HardStopsJson { get; set; } = "[]";
    public string RequiredApprovalsJson { get; set; } = "[]";
    public string PolicyVersion { get; set; } = string.Empty;
    public string RulesVersion { get; set; } = string.Empty;
    public DateTime EvaluatedAt { get; set; }
    public string EvaluatedBy { get; set; } = "system";
}

/// <summary>
/// Controlled identity-correction transaction. Direct edits to DIN/product/ABO/expiration
/// after clinical workflow are blocked; corrections go through this audited path.
/// </summary>
public class BloodComponentIdentityCorrection : BaseEntity
{
    public long BloodProductId { get; set; }
    public BloodUnit? Unit { get; set; }
    public string Field { get; set; } = string.Empty;
    public string OriginalValue { get; set; } = string.Empty;
    public string CorrectedValue { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string CorrectedBy { get; set; } = "system";
    public string? ApproverId { get; set; }
    public DateTime CorrectedAt { get; set; }
    public string? SupportingEvidence { get; set; }
    public string? AffectedTransactionsJson { get; set; }
    public bool RevalidationRequired { get; set; } = true;
    public bool RevalidationCompleted { get; set; }
}

public class BloodComponentException : BaseEntity
{
    public long BloodProductId { get; set; }
    public BloodUnit? Unit { get; set; }
    public string ExceptionCode { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Severity { get; set; } = "ERROR";
    public string? OverrideCode { get; set; }
    public string? OverrideReason { get; set; }
    public string? ApproverId { get; set; }
    public string RecordedBy { get; set; } = "system";
    public DateTime RecordedAt { get; set; }
}

public class CompatibilityRuleVersion : BaseEntity
{
    public string Version { get; set; } = string.Empty;
    public string PolicyVersion { get; set; } = string.Empty;
    public DateOnly EffectiveDate { get; set; }
    public DateOnly? RetiredDate { get; set; }
    public bool IsActive { get; set; } = true;
    public string Notes { get; set; } = "INSTITUTIONAL_POLICY_REVIEW required.";
    public ICollection<CompatibilityRule> Rules { get; set; } = new List<CompatibilityRule>();
}

/// <summary>
/// Table-driven compatibility rule. Exact clinical tables are validated configuration data,
/// not generic software constants. MEDICAL_DIRECTOR_APPROVAL required.
/// </summary>
public class CompatibilityRule : BaseEntity
{
    public long CompatibilityRuleVersionId { get; set; }
    public CompatibilityRuleVersion? Version { get; set; }
    public string RuleCode { get; set; } = string.Empty;
    public ComponentClass ComponentClass { get; set; }
    public string RuleFamily { get; set; } = "RedBloodCells";
    public string ExpressionJson { get; set; } = "{}";
    public string Severity { get; set; } = "HardStop";
    public bool IsActive { get; set; } = true;
    public string Description { get; set; } = string.Empty;
}
