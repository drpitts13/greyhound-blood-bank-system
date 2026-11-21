using BloodBankLIS.Domain.Common;

namespace BloodBankLIS.Domain.Entities;

/// <summary>
/// One time a tube was put away or moved (rack, shelf, refrigerator position). The tube keeps its current place on
/// <see cref="Specimen.StorageLocation"/>; these rows keep every earlier one, so a move never overwrites where it was.
/// </summary>
public class SpecimenStorageEvent : BaseEntity
{
    public long SpecimenId { get; set; }

    public string Location { get; set; } = string.Empty;

    /// <summary>Where the tube was before, or null the first time it was stored.</summary>
    public string? PreviousLocation { get; set; }

    public DateTime StoredUtc { get; set; }

    public string StoredBy { get; set; } = string.Empty;

    public string? Note { get; set; }

    /// <summary>The tube was discarded here (Location is "Discarded"); not a storage place.</summary>
    public bool Discarded { get; set; }
}
