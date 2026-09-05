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

    /// <summary>CPT or HCPCS procedure code snapshotted onto DFT FT1-25.</summary>
    public string? CptCode { get; set; }

    /// <summary>UB-04 revenue code (typically 4 digits) mapped to DFT FT1-13.</summary>
    public string? RevenueCode { get; set; }

    /// <summary>CPT/HCPCS modifier mapped to DFT FT1-26.</summary>
    public string? Modifier { get; set; }

    public bool IsActive { get; set; } = true;
}
