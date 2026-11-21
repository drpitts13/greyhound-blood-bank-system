using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Application.Inventory;

/// <summary>The scans made so far at one location. <see cref="Finish"/> records the count in the audit trail.</summary>
public sealed record InventoryCountRequest(long LocationId, IReadOnlyList<string> Scans, bool Finish = false);

public enum InventoryCountOutcome
{
    /// <summary>Recorded here and scanned.</summary>
    Found = 0,
    /// <summary>Recorded here, not scanned.</summary>
    Missing = 1,
    /// <summary>Scanned here, on hand, but recorded in another location (or none).</summary>
    Elsewhere = 2,
    /// <summary>Scanned here, but its status says it is not on the shelf (issued, transfused, discarded, ...).</summary>
    NotOnHand = 3
}

public sealed record InventoryCountLineDto(
    long UnitId,
    string UnitNumber,
    string? ProductName,
    string BloodType,
    UnitStatus Status,
    DateTime ExpiresUtc,
    InventoryCountOutcome Outcome,
    string? RecordedLocation)
{
    /// <summary>On the shelf but past its expiry (or marked Expired): to be discarded.</summary>
    public bool Expired { get; init; }

    /// <summary>Why this location should not hold the unit (it does not take the component), or null.</summary>
    public string? WrongStorage { get; init; }
}

/// <summary>A finished stock count, from the audit trail.</summary>
public sealed record InventoryCountHistoryDto(
    long LocationId,
    string LocationName,
    DateTime CountedUtc,
    string CountedBy,
    string Summary,
    bool Reconciled);

/// <summary>A scan that names no unit, or a divided number matching several units.</summary>
public sealed record InventoryCountProblemDto(string Scan, string Message);

public sealed record InventoryCountDto(
    long LocationId,
    string LocationName,
    int Expected,
    int Scanned,
    IReadOnlyList<InventoryCountLineDto> Found,
    IReadOnlyList<InventoryCountLineDto> Missing,
    IReadOnlyList<InventoryCountLineDto> Elsewhere,
    IReadOnlyList<InventoryCountLineDto> NotOnHand,
    IReadOnlyList<InventoryCountProblemDto> Problems,
    bool Recorded)
{
    /// <summary>Everything recorded here was scanned and nothing unexpected turned up.</summary>
    public bool Reconciled => Missing.Count == 0 && Elsewhere.Count == 0 && NotOnHand.Count == 0 && Problems.Count == 0;
}
