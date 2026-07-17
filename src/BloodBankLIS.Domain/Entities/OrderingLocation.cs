using BloodBankLIS.Domain.Common;

namespace BloodBankLIS.Domain.Entities;

/// <summary>
/// A location from which blood bank orders are placed (ward, OR, ED, etc.).
/// </summary>
public class OrderingLocation : BaseEntity
{
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Department { get; set; }

    public string? Hl7MappingCode { get; set; }

    public bool IsActive { get; set; } = true;
}
