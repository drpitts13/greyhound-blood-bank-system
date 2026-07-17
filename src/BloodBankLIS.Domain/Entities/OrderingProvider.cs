using BloodBankLIS.Domain.Common;

namespace BloodBankLIS.Domain.Entities;

/// <summary>
/// A clinician who may place orders or attend a patient visit. Provider id is the
/// preserved source identifier (HL7 XCN/id) and is unique.
/// </summary>
public class OrderingProvider : BaseEntity
{
    public string ProviderId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Specialty { get; set; }

    public string? Location { get; set; }

    public bool IsActive { get; set; } = true;

    public string? SourceSystem { get; set; }
}
