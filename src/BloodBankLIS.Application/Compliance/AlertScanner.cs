using BloodBankLIS.Application.Inventory;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Application.Compliance;

/// <summary>
/// Turns existing worklists into alerts. It reads minimum stock shortfalls, near-expiry units,
/// and reagent QC work, and changes nothing on those records.
/// </summary>
public sealed class AlertScanner
{
    private readonly AlertService _alerts;
    private readonly MinimumStockService _minimumStock;
    private readonly InventoryService _inventory;
    private readonly ReagentQcService _reagents;

    public AlertScanner(AlertService alerts, MinimumStockService minimumStock, InventoryService inventory, ReagentQcService reagents)
    {
        _alerts = alerts;
        _minimumStock = minimumStock;
        _inventory = inventory;
        _reagents = reagents;
    }

    public async Task<(int Raised, int Resolved)> ScanAsync(CancellationToken ct = default)
    {
        var raised = 0;
        var resolved = 0;

        var shortages = (await _minimumStock.ListAsync(ct))
            .Where(row => row.Shortfall > 0)
            .Select(row => new AlertCandidate(
                AlertRule.MinimumStockShortage,
                $"PAR:{row.MinimumStockId}", // Stored key prefix, kept so open alerts still match.
                AlertSeverity.Warning,
                $"Below minimum stock: {row.ProductCode}{(row.Abo is null ? "" : $" group {row.Abo}")}",
                $"{row.OnHand} available against a minimum of {row.MinimumAvailable} (short {row.Shortfall}).",
                "/inventory/minimum-stock"))
            .ToList();
        Add(await _alerts.SyncAsync(AlertRule.MinimumStockShortage, shortages, ct));

        var nearExpiry = (await _inventory.ListNearExpiryAsync(ct))
            .Select(unit => new AlertCandidate(
                AlertRule.NearExpiry,
                $"NEAREXP:{unit.UnitId}",
                AlertSeverity.Info,
                $"Near expiry: {unit.UnitNumber}",
                $"Expires {unit.ExpiresUtc:yyyy-MM-dd HH:mm} UTC at {unit.LocationLabel}.",
                $"/inventory/units/{unit.UnitId}"))
            .ToList();
        Add(await _alerts.SyncAsync(AlertRule.NearExpiry, nearExpiry, ct));

        var qc = (await _reagents.ListWorkAsync(ct))
            .Select(item => new AlertCandidate(
                AlertRule.ReagentQc,
                $"QC:{item.Kind}:{item.LotId}:{item.QcId}",
                item.Kind == ReagentQcWorklistRule.Failed ? AlertSeverity.Critical : AlertSeverity.Warning,
                $"Reagent QC {item.Kind.ToLowerInvariant()}: lot {item.LotNumber}",
                item.Message,
                "/reagents"))
            .ToList();
        Add(await _alerts.SyncAsync(AlertRule.ReagentQc, qc, ct));

        return (raised, resolved);

        void Add((int Raised, int Resolved) result)
        {
            raised += result.Raised;
            resolved += result.Resolved;
        }
    }

    /// <summary>Event alert after the scheduled expiration sweep moved units to Expired.</summary>
    public Task<bool> RaiseUnitsExpiredAsync(int count, DateTime sweptUtc, CancellationToken ct = default) =>
        count <= 0
            ? Task.FromResult(false)
            : _alerts.RaiseAsync(new AlertCandidate(
                AlertRule.UnitsExpired,
                $"EXPIRED:{sweptUtc:yyyyMMddHHmmss}",
                AlertSeverity.Warning,
                $"{count} unit(s) expired",
                $"The scheduled sweep moved {count} unit(s) to Expired at {sweptUtc:yyyy-MM-dd HH:mm} UTC. Remove them from storage.",
                "/inventory?status=Expired"), ct);
}
