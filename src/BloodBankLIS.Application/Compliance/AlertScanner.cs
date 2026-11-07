using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Inventory;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
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
    private readonly IRepository<TransfusionEvent>? _transfusions;
    private readonly IRepository<Issue>? _issues;
    private readonly IRepository<BloodUnit>? _units;
    private readonly IRepository<Patient>? _patients;
    private readonly FacilityPolicyService? _policy;
    private readonly IClock? _clock;

    public AlertScanner(
        AlertService alerts,
        MinimumStockService minimumStock,
        InventoryService inventory,
        ReagentQcService reagents,
        IRepository<TransfusionEvent>? transfusions = null,
        IRepository<Issue>? issues = null,
        IRepository<BloodUnit>? units = null,
        IRepository<Patient>? patients = null,
        FacilityPolicyService? policy = null,
        IClock? clock = null)
    {
        _alerts = alerts;
        _minimumStock = minimumStock;
        _inventory = inventory;
        _reagents = reagents;
        _transfusions = transfusions;
        _issues = issues;
        _units = units;
        _patients = patients;
        _policy = policy;
        _clock = clock;
    }

    /// <summary>"2 available against a minimum of 4 (short 2). 1 on order, 1 still to order."</summary>
    public static string ShortageMessage(MinimumStockRowDto row)
    {
        var text = $"{row.OnHand} available against a minimum of {row.MinimumAvailable} (short {row.Shortfall}).";
        if (row.OnOrder <= 0)
        {
            return text;
        }

        var state = row.OnOrderOverdue > 0
            ? $"{row.OnOrderOverdue} overdue: chase the supplier"
            : row.StillToOrder == 0 ? "covering the shortage" : $"{row.StillToOrder} still to order";
        return $"{text} {row.OnOrder} on order, {state}.";
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
                // Covered by orders still on time: information. Anything left to order, or a late order: a warning.
                row.StillToOrder == 0 && row.OnOrderOverdue == 0 ? AlertSeverity.Info : AlertSeverity.Warning,
                $"Below minimum stock: {row.ProductCode}{(row.Abo is null ? "" : $" group {row.Abo}")}",
                ShortageMessage(row),
                "/inventory/minimum-stock"))
            .ToList();
        Add(await _alerts.SyncAsync(AlertRule.MinimumStockShortage, shortages, ct));

        var nearExpiry = (await _inventory.ListNearExpiryAsync(ct))
            .Select(unit => new AlertCandidate(
                AlertRule.NearExpiry,
                $"NEAREXP:{unit.UnitId}",
                unit.IsReserved ? AlertSeverity.Warning : AlertSeverity.Info,
                $"Near expiry: {unit.UnitNumber}",
                $"Expires {unit.ExpiresUtc:yyyy-MM-dd HH:mm} UTC at {unit.LocationLabel}." +
                (unit.IsReserved
                    ? $" Reserved for {unit.ReservedPatientLabel}. {NearExpiryWorkItemDto.ReservedNextStep}"
                    : string.Empty),
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

        if (await TransfusionsNotCompletedAsync(ct) is { } incomplete)
        {
            Add(await _alerts.SyncAsync(AlertRule.TransfusionNotCompleted, incomplete, ct));
        }

        return (raised, resolved);

        void Add((int Raised, int Resolved) result)
        {
            raised += result.Raised;
            resolved += result.Resolved;
        }
    }

    /// <summary>
    /// Transfusions started at the bedside (record with no stop on an issue still Issued) and not completed within
    /// the facility reminder hours. Null when the scanner was built without these sources.
    /// </summary>
    private async Task<List<AlertCandidate>?> TransfusionsNotCompletedAsync(CancellationToken ct)
    {
        if (_transfusions is null || _issues is null || _units is null || _patients is null || _policy is null || _clock is null)
        {
            return null;
        }

        var hours = await _policy.GetTransfusionCompletionReminderHoursAsync(ct);
        var now = _clock.UtcNow;
        var open = (await _transfusions.ListAsync(t => t.StopUtc == null && t.StartUtc != null, ct))
            .Where(t => TransfusionCompletionRule.IsOverdue(t.StartUtc, now, hours))
            .ToList();
        var candidates = new List<AlertCandidate>();
        foreach (var record in open)
        {
            var issue = await _issues.GetByIdAsync(record.IssueId, ct);
            if (issue is null || issue.Status != IssueStatus.Issued)
            {
                continue;
            }

            var unit = await _units.GetByIdAsync(record.BloodProductId, ct);
            var patient = await _patients.GetByIdAsync(record.PatientId, ct);
            candidates.Add(TransfusionCompletionRule.Alert(
                issue.Id, unit?.UnitNumber ?? $"unit {record.BloodProductId}", patient?.MedicalRecordNumber, record.StartUtc!.Value));
        }

        return candidates;
    }

    /// <summary>Event alert after the scheduled expiration sweep moved units to Expired.</summary>
    public Task<bool> RaiseUnitsExpiredAsync(int count, DateTime sweptUtc, int reservationsEnded = 0, CancellationToken ct = default) =>
        count <= 0
            ? Task.FromResult(false)
            : _alerts.RaiseAsync(new AlertCandidate(
                AlertRule.UnitsExpired,
                $"EXPIRED:{sweptUtc:yyyyMMddHHmmss}",
                AlertSeverity.Warning,
                $"{count} unit(s) expired",
                $"The scheduled sweep moved {count} unit(s) to Expired at {sweptUtc:yyyy-MM-dd HH:mm} UTC. Remove them from storage." +
                (reservationsEnded > 0
                    ? $" {reservationsEnded} of them were reserved for patients; those reservations have ended. Reserve replacement units."
                    : string.Empty),
                "/inventory?status=Expired"), ct);
}
