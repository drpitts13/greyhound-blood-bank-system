namespace BloodBankLIS.Application.Inventory;

/// <summary>Move the scanned unit to a location (the Move units desk).</summary>
public sealed record TransferByScanRequest(string? Scan, long ToLocationId, string? Reason = null);
