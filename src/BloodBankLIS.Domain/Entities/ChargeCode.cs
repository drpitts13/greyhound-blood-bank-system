using BloodBankLIS.Domain.Common;

namespace BloodBankLIS.Domain.Entities;

/// <summary>
/// A billable charge code (table <c>ChargeCodes</c>). <see cref="Code"/> is unique.
/// <see cref="CptCode"/> is a placeholder for future claim mapping (docs B.1).
/// </summary>
public class ChargeCode : BaseEntity
{
    public string Code { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public decimal DefaultAmount { get; set; }

    /// <summary>Placeholder CPT mapping for a future billing export phase.</summary>
    public string? CptCode { get; set; }

    public bool IsActive { get; set; } = true;
}
