using BloodBankLIS.Domain.Common;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Entities.Configuration;

/// <summary>
/// A cooler or other transport vessel the blood bank packs units into (Admin › Transport Containers). None are
/// shipped. The temperature range and the hours a container may stay out are the facility's own validation results;
/// the software does not supply them. When entered, a packing temperature outside the range is flagged on the
/// pack record and its report.
/// </summary>
public class TransportContainer : BaseEntity
{
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public TransportContainerType ContainerType { get; set; } = TransportContainerType.Cooler;

    public string? Description { get; set; }

    public decimal? MinTemperatureC { get; set; }

    public decimal? MaxTemperatureC { get; set; }

    /// <summary>Hours from packing the facility has validated this container for; printed on the pack report.</summary>
    public int? ValidatedHours { get; set; }

    public int SortOrder { get; set; }

    public bool IsActive { get; set; } = true;
}
