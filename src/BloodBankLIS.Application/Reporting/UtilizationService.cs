using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Application.Reporting;

public sealed record UtilizationRowDto(
    string OrderingLocation,
    string OrderingProvider,
    int Crossmatched,
    int Issued,
    int Transfused,
    decimal? CrossmatchToTransfusionRatio,
    decimal? TransfusedPercentOfCrossmatched,
    decimal? TransfusedPercentOfIssued);

public sealed record UtilizationReportDto(
    DateTime FromUtc,
    DateTime ToUtc,
    UtilizationRowDto Total,
    IReadOnlyList<UtilizationRowDto> Rows)
{
    /// <summary>Issues, transfusions, returns and discards in the period by product (end-user testing pass 262).</summary>
    public IReadOnlyList<ProductUtilizationRowDto> ByProduct { get; init; } = [];
}

/// <summary>
/// One product's use in the period: units issued (of them, emergency or massive-transfusion issues, which are never
/// crossmatched first), issued units transfused, issued units returned, and units discarded.
/// </summary>
public sealed record ProductUtilizationRowDto(string Product, int Issued, int EmergencyIssued, int Transfused, int Returned, int Discarded)
{
    public decimal? TransfusedPercentOfIssued => UtilizationRule.Percent(Transfused, Issued);
}

/// <summary>
/// Crossmatch-to-transfusion utilization by ordering location and provider.
/// The period is the crossmatch time. Issue and transfusion are counted for the same
/// patient and unit whenever they happened, so a unit crossmatched late in the period
/// still counts when it was transfused after it.
/// </summary>
public sealed class UtilizationService
{
    public const string NoOrder = "(no order)";
    public const string NoProvider = "(no provider)";

    private readonly IRepository<Crossmatch> _crossmatches;
    private readonly IRepository<Issue> _issues;
    private readonly IRepository<TransfusionEvent> _transfusions;
    private readonly IRepository<Order> _orders;
    private readonly IRepository<OrderingLocation> _locations;
    private readonly IRepository<OrderingProvider> _providers;
    private readonly IClock _clock;
    private readonly IRepository<BloodUnit>? _units;
    private readonly IRepository<ProductType>? _productTypes;
    private readonly IInventoryRepository? _inventory;

    public UtilizationService(
        IRepository<Crossmatch> crossmatches,
        IRepository<Issue> issues,
        IRepository<TransfusionEvent> transfusions,
        IRepository<Order> orders,
        IRepository<OrderingLocation> locations,
        IRepository<OrderingProvider> providers,
        IClock clock,
        IRepository<BloodUnit>? units = null,
        IRepository<ProductType>? productTypes = null,
        IInventoryRepository? inventory = null)
    {
        _units = units;
        _productTypes = productTypes;
        _inventory = inventory;
        _crossmatches = crossmatches;
        _issues = issues;
        _transfusions = transfusions;
        _orders = orders;
        _locations = locations;
        _providers = providers;
        _clock = clock;
    }

    public async Task<UtilizationReportDto> GetAsync(DateTime? fromUtc, DateTime? toUtc, CancellationToken ct = default)
    {
        var to = toUtc ?? _clock.UtcNow;
        var from = fromUtc ?? to.AddDays(-30);
        if (from > to)
        {
            (from, to) = (to, from);
        }

        var crossmatches = await _crossmatches.ListAsync(
            c => c.Result == CrossmatchResult.Compatible && c.PerformedUtc >= from && c.PerformedUtc <= to,
            ct);

        // One row per patient and unit; the earliest crossmatch decides which order it belongs to.
        var pairs = crossmatches
            .GroupBy(c => (c.PatientId, c.BloodProductId))
            .Select(g => g.OrderBy(c => c.PerformedUtc).ThenBy(c => c.Id).First())
            .ToList();

        var patientIds = pairs.Select(p => p.PatientId).Distinct().ToList();
        var patientIssues = await _issues.ListAsync(i => patientIds.Contains(i.PatientId), ct);
        var issued = patientIssues
            .Select(i => (i.PatientId, i.BloodProductId))
            .ToHashSet();
        var issueStatusById = patientIssues.ToDictionary(i => i.Id, i => i.Status);
        var transfused = (await _transfusions.ListAsync(t => patientIds.Contains(t.PatientId), ct))
            .Where(t => !TransfusionRecordState.IsRunning(t.StopUtc, issueStatusById.GetValueOrDefault(t.IssueId, IssueStatus.Transfused))
                && UtilizationRule.CountsAsTransfused(t.FinalDisposition))
            .Select(t => (t.PatientId, t.BloodProductId))
            .ToHashSet();

        var orderIds = pairs.Where(p => p.OrderId != null).Select(p => p.OrderId!.Value).Distinct().ToList();
        var orders = (await _orders.ListAsync(o => orderIds.Contains(o.Id), ct)).ToDictionary(o => o.Id);
        var locations = (await _locations.ListAsync(ct)).ToDictionary(l => l.Id, l => l.Name);
        var providers = (await _providers.ListAsync(ct)).ToDictionary(p => p.Id, p => p.Name);

        var facts = pairs.Select(p =>
        {
            orders.TryGetValue(p.OrderId ?? 0, out var order);
            var location = order is null ? NoOrder : locations.GetValueOrDefault(order.OrderingLocationId, NoOrder);
            var provider = order is null
                ? NoProvider
                : order.OrderingProviderId is long providerId && providers.TryGetValue(providerId, out var name)
                    ? name
                    : string.IsNullOrWhiteSpace(order.OrderingProvider) ? NoProvider : order.OrderingProvider.Trim();
            var key = (p.PatientId, p.BloodProductId);
            return (Location: location, Provider: provider, Issued: issued.Contains(key), Transfused: transfused.Contains(key));
        }).ToList();

        var rows = facts
            .GroupBy(f => (f.Location, f.Provider))
            .Select(g => Row(g.Key.Location, g.Key.Provider, g.Count(), g.Count(f => f.Issued), g.Count(f => f.Transfused)))
            .OrderBy(r => r.OrderingLocation, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.OrderingProvider, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var total = Row("All locations", "All providers", facts.Count, facts.Count(f => f.Issued), facts.Count(f => f.Transfused));
        return new UtilizationReportDto(from, to, total, rows) { ByProduct = await ByProductAsync(from, to, ct) };
    }

    /// <summary>
    /// End-user testing pass 262: the page counted only crossmatched units, so plasma, platelets and the four units issued
    /// uncrossmatched under MTP #1 were not on it, and nothing said how many units were wasted. Issues are counted by issue
    /// time; discards by the time the unit was discarded.
    /// </summary>
    private async Task<IReadOnlyList<ProductUtilizationRowDto>> ByProductAsync(DateTime from, DateTime to, CancellationToken ct)
    {
        if (_units is null || _productTypes is null)
        {
            return [];
        }

        var issues = await _issues.ListAsync(i => i.IssuedUtc >= from && i.IssuedUtc <= to, ct);
        var issueIds = issues.Select(i => i.Id).ToHashSet();
        var issueStatusById = issues.ToDictionary(i => i.Id, i => i.Status);
        var transfusedIssueIds = (await _transfusions.ListAsync(t => issueIds.Contains(t.IssueId), ct))
            .Where(t => !TransfusionRecordState.IsRunning(t.StopUtc, issueStatusById.GetValueOrDefault(t.IssueId, IssueStatus.Transfused))
                && UtilizationRule.CountsAsTransfused(t.FinalDisposition))
            .Select(t => t.IssueId)
            .ToHashSet();

        // A unit discarded in the period was last changed no earlier than its discard.
        var discardedCandidates = await _units.ListAsync(u => u.Status == UnitStatus.Discarded && u.ModifiedUtc != null && u.ModifiedUtc >= from, ct);
        var discarded = new List<BloodUnit>();
        foreach (var unit in discardedCandidates)
        {
            var at = _inventory is null
                ? unit.ModifiedUtc
                : (await _inventory.GetHistoryAsync(unit.Id, ct)).Where(h => h.ToStatus == UnitStatus.Discarded).Select(h => (DateTime?)h.ChangedUtc).Max();
            if (at is DateTime when && when >= from && when <= to)
            {
                discarded.Add(unit);
            }
        }

        var unitIds = issues.Select(i => i.BloodProductId).Concat(discarded.Select(u => u.Id)).Distinct().ToList();
        var units = (await _units.ListAsync(u => unitIds.Contains(u.Id), ct)).ToDictionary(u => u.Id);
        var products = (await _productTypes.ListAsync(ct)).ToDictionary(p => p.Id, p => p.Name);
        string Product(long unitId) =>
            units.TryGetValue(unitId, out var u) && products.TryGetValue(u.ProductTypeId, out var name) ? name : "Unknown product";

        var issued = issues.GroupBy(i => Product(i.BloodProductId)).ToDictionary(g => g.Key, g => g.ToList());
        var wasted = discarded.GroupBy(u => Product(u.Id)).ToDictionary(g => g.Key, g => g.Count());
        return issued.Keys.Concat(wasted.Keys).Distinct()
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .Select(p =>
            {
                var list = issued.GetValueOrDefault(p) ?? [];
                return new ProductUtilizationRowDto(
                    p,
                    list.Count,
                    list.Count(i => i.IssueType is IssueType.EmergencyRelease or IssueType.MassiveTransfusion),
                    list.Count(i => transfusedIssueIds.Contains(i.Id)),
                    list.Count(i => i.Status == IssueStatus.Returned),
                    wasted.GetValueOrDefault(p));
            })
            .ToList();
    }

    private static UtilizationRowDto Row(string location, string provider, int crossmatched, int issued, int transfused) =>
        new(location, provider, crossmatched, issued, transfused,
            UtilizationRule.CrossmatchToTransfusionRatio(crossmatched, transfused),
            UtilizationRule.Percent(transfused, crossmatched),
            UtilizationRule.Percent(transfused, issued));
}
