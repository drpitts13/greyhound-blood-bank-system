using BloodBankLIS.Domain.Common;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Entities;

/// <summary>
/// Unit-scoped ABO/Rh retype result. Patient <c>TestResults</c> cannot be reused
/// because they require an order line, specimen, and patient.
/// </summary>
public class ProductRetypeResult : BaseEntity
{
    public long BloodProductId { get; set; }

    public BloodUnit? Unit { get; set; }

    public long TestDefinitionId { get; set; }

    public TestDefinition? TestDefinition { get; set; }

    public string TestCode { get; set; } = "ABORH-RETYPE";

    /// <summary>ABORH panel JSON (front types only).</summary>
    public string Value { get; set; } = string.Empty;

    public AboGroup InterpretedAbo { get; set; } = AboGroup.Unknown;

    /// <summary>Null when Anti-D was not performed (typical for labeled Rh-positive units).</summary>
    public RhType? InterpretedRh { get; set; }

    public bool MatchesLabel { get; set; }

    public string? DiscrepancyDetail { get; set; }

    public ResultStatus Status { get; set; } = ResultStatus.Verified;

    public string EnteredBy { get; set; } = string.Empty;

    public DateTime EnteredUtc { get; set; }

    public string? VerifiedBy { get; set; }

    public DateTime? VerifiedUtc { get; set; }
}
