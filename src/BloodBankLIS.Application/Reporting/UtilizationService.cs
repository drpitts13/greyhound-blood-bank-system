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
    IReadOnlyList<UtilizationRowDto> Rows);

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

    public UtilizationService(
        IRepository<Crossmatch> crossmatches,
        IRepository<Issue> issues,
        IRepository<TransfusionEvent> transfusions,
        IRepository<Order> orders,
        IRepository<OrderingLocation> locations,
        IRepository<OrderingProvider> providers,
        IClock clock)
    {
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
        var issued = (await _issues.ListAsync(i => patientIds.Contains(i.PatientId), ct))
            .Select(i => (i.PatientId, i.BloodProductId))
            .ToHashSet();
        var transfused = (await _transfusions.ListAsync(t => patientIds.Contains(t.PatientId), ct))
            .Where(t => UtilizationRule.CountsAsTransfused(t.FinalDisposition))
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
        return new UtilizationReportDto(from, to, total, rows);
    }

    private static UtilizationRowDto Row(string location, string provider, int crossmatched, int issued, int transfused) =>
        new(location, provider, crossmatched, issued, transfused,
            UtilizationRule.CrossmatchToTransfusionRatio(crossmatched, transfused),
            UtilizationRule.Percent(transfused, crossmatched),
            UtilizationRule.Percent(transfused, issued));
}
