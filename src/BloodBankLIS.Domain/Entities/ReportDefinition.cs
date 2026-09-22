using BloodBankLIS.Domain.Common;
using BloodBankLIS.Domain.Reporting;

namespace BloodBankLIS.Domain.Entities;

/// <summary>
/// A saved operational report. Visual definitions store a layout JSON document.
/// SQL definitions store one SELECT against reporting views. System-owned rows
/// are the prebuilt starter set and cannot be deleted.
/// </summary>
public class ReportDefinition : BaseEntity
{
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public ReportKind Kind { get; set; } = ReportKind.Visual;

    /// <summary>Prebuilt reports. Staff may clone them; they cannot be deleted or overwritten.</summary>
    public bool IsSystemOwned { get; set; }

    /// <summary>Serialized <c>ReportLayout</c> when <see cref="Kind"/> is Visual.</summary>
    public string? LayoutJson { get; set; }

    /// <summary>Single SELECT when <see cref="Kind"/> is Sql.</summary>
    public string? SqlText { get; set; }
}
