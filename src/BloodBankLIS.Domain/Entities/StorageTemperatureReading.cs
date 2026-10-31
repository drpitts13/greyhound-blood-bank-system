using BloodBankLIS.Domain.Common;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Entities;

/// <summary>
/// One temperature reading for a storage location. A reading outside the location's
/// configured range is an excursion and opens a deviation. Readings are never edited:
/// a wrong reading is followed by a corrected one.
/// </summary>
public class StorageTemperatureReading : BaseEntity
{
    public long InventoryLocationId { get; set; }

    public InventoryLocation? InventoryLocation { get; set; }

    public decimal ReadingC { get; set; }

    public DateTime ReadingUtc { get; set; }

    public string RecordedBy { get; set; } = string.Empty;

    /// <summary>Range in force when the reading was taken, kept so a later range change does not rewrite history.</summary>
    public decimal? RangeMinC { get; set; }

    public decimal? RangeMaxC { get; set; }

    public StorageTemperatureStatus Status { get; set; }

    public long? DeviationId { get; set; }

    public string? Comment { get; set; }
}
