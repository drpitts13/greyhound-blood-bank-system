using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Application.Inventory;

/// <summary>Move the scanned unit to a location (the Move units desk).</summary>
public sealed record TransferByScanRequest(string? Scan, long ToLocationId, string? Reason = null);

/// <summary>An expired unit scanned for disposal; the second verifier when the facility requires one.</summary>
public sealed record DiscardExpiredByScanRequest(string? Scan, string? SecondVerifier = null);

/// <summary>An expired unit still on a shelf, waiting to be discarded.</summary>
public sealed record ExpiredUnitDto(
    long UnitId,
    string UnitNumber,
    string? ProductCode,
    string? ProductName,
    string BloodType,
    UnitStatus Status,
    DateTime ExpiresUtc,
    string? LocationName);
