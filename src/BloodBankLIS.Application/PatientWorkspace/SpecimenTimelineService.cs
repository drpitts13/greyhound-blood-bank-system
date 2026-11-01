using System.Text.Json;
using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Domain.Audit;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Application.PatientWorkspace;

public sealed record SpecimenTimelineDto(
    long SpecimenId,
    long PatientId,
    string AccessionNumber,
    SpecimenStatus Status,
    IReadOnlyList<TimelineEventDto> Events);

/// <summary>
/// Everything that happened to one specimen, newest first: collection, each audited state change (accession,
/// receipt, acceptance, rejection, relabel, edits) with who and which workstation, expiry overrides, results on the
/// tube, the orders it serves, and the crossmatches and reservations that used it. Read-only.
/// </summary>
public sealed class SpecimenTimelineService
{
    private const int MaxChangesShown = 4;

    private readonly IRepository<Specimen> _specimens;
    private readonly IRepository<TestResult> _results;
    private readonly IRepository<OrderSpecimen> _orderSpecimens;
    private readonly IRepository<Order> _orders;
    private readonly IRepository<Crossmatch> _crossmatches;
    private readonly IRepository<Allocation> _allocations;
    private readonly IRepository<BloodUnit> _units;
    private readonly IRepository<Override> _overrides;
    private readonly IAuditQuery _audit;
    private readonly IClock _clock;

    public SpecimenTimelineService(
        IRepository<Specimen> specimens,
        IRepository<TestResult> results,
        IRepository<OrderSpecimen> orderSpecimens,
        IRepository<Order> orders,
        IRepository<Crossmatch> crossmatches,
        IRepository<Allocation> allocations,
        IRepository<BloodUnit> units,
        IRepository<Override> overrides,
        IAuditQuery audit,
        IClock clock)
    {
        _specimens = specimens;
        _results = results;
        _orderSpecimens = orderSpecimens;
        _orders = orders;
        _crossmatches = crossmatches;
        _allocations = allocations;
        _units = units;
        _overrides = overrides;
        _audit = audit;
        _clock = clock;
    }

    public async Task<SpecimenTimelineDto?> GetAsync(long specimenId, CancellationToken ct = default)
    {
        var specimen = (await _specimens.ListAsync(s => s.Id == specimenId, ct)).FirstOrDefault();
        if (specimen is null)
        {
            return null;
        }

        var chart = $"/patients/{specimen.PatientId}";
        var label = specimen.AccessionNumber;
        var events = new List<TimelineEventDto>
        {
            new(specimen.CollectedUtc, TimelineCategory.Specimen, TimelineSeverity.Info,
                $"Collected ({specimen.SpecimenType})",
                PatientTimelineService.Join(
                    string.IsNullOrWhiteSpace(specimen.DrawLocation) ? null : $"At {specimen.DrawLocation}",
                    IdentifierText(specimen)),
                specimen.Collector, null, $"spc-{specimen.Id}-collect")
        };

        // Only rows the specimen workflow wrote on purpose. The generic Create/Update rows the database layer adds
        // on every save repeat the whole record and would bury the meaningful changes.
        var audit = (await _audit.ListForEntityAsync(nameof(Specimen), specimen.Id, ct))
            .Where(a => a.EventType is not (AuditEventType.Create or AuditEventType.Update or AuditEventType.Delete))
            .ToList();
        var auditedReceipt = false;
        foreach (var row in audit)
        {
            var changes = Changes(row.OldValueJson, row.NewValueJson);
            auditedReceipt |= row.NewValueJson?.Contains("\"ReceivedUtc\"", StringComparison.Ordinal) == true;
            var reason = string.IsNullOrWhiteSpace(row.Reason) ? "Specimen record changed" : row.Reason.Trim().TrimEnd('.');
            var attention = row.NewValueJson?.Contains("\"RejectionReason\"", StringComparison.Ordinal) == true
                || reason.StartsWith("Specimen relabeled", StringComparison.OrdinalIgnoreCase);
            events.Add(new(row.OccurredUtc, TimelineCategory.Specimen, attention ? TimelineSeverity.Warning : TimelineSeverity.Info,
                reason,
                changes,
                string.IsNullOrWhiteSpace(row.Workstation) ? row.UserName : $"{row.UserName} at {row.Workstation}",
                null,
                $"aud-{row.Id}"));
        }

        // Records made before audit rows existed (or seeded data) still show their receipt time.
        if (!auditedReceipt && specimen.ReceivedUtc is DateTime received)
        {
            events.Add(new(received, TimelineCategory.Specimen, TimelineSeverity.Info,
                "Received in the blood bank", null, null, null, $"spc-{specimen.Id}-receive"));
        }

        if (specimen.ExpiresUtc is DateTime expires)
        {
            var expired = expires <= _clock.UtcNow;
            events.Add(new(expires, TimelineCategory.Specimen, expired ? TimelineSeverity.Warning : TimelineSeverity.Info,
                expired ? "Specimen expired" : "Specimen expires",
                expired ? "Not usable for crossmatch or new testing after this time" : "Scheduled; not yet reached",
                null, null, $"spc-{specimen.Id}-expiry"));
        }

        foreach (var o in await _overrides.ListAsync(o => o.ContextType == nameof(Specimen) && o.ContextId == specimen.Id, ct))
        {
            events.Add(new(o.OverriddenUtc, TimelineCategory.Alert, TimelineSeverity.Warning,
                $"Override {o.RuleCode}",
                PatientTimelineService.Join($"Reason: {o.Reason}", string.IsNullOrWhiteSpace(o.AuthorizedBy) ? null : $"authorized by {o.AuthorizedBy}"),
                o.CreatedBy, null, $"ovr-{o.Id}"));
        }

        var links = await _orderSpecimens.ListAsync(l => l.SpecimenId == specimen.Id, ct);
        var orderIds = links.Select(l => l.OrderId).Distinct().ToList();
        var orders = orderIds.Count == 0 ? [] : await _orders.ListAsync(o => orderIds.Contains(o.Id), ct);
        foreach (var o in orders)
        {
            events.Add(new(o.OrderedUtc, TimelineCategory.Order, o.Status == OrderStatus.Cancelled ? TimelineSeverity.Warning : TimelineSeverity.Info,
                $"Serves order {o.OrderNumber}: {(string.IsNullOrWhiteSpace(o.OrderName) ? o.TestCode : o.OrderName)}",
                PatientTimelineService.Join($"{o.Priority}", $"Now {o.Status}",
                    o.Status == OrderStatus.Cancelled && !string.IsNullOrWhiteSpace(o.CancellationReason) ? $"Cancelled: {o.CancellationReason}" : null),
                o.OrderingProvider ?? o.CreatedBy, $"{chart}?tab=orders", $"ord-{o.Id}"));
        }

        foreach (var r in await _results.ListAsync(r => r.SpecimenId == specimen.Id, ct))
        {
            events.AddRange(PatientTimelineService.ResultEvents(r, label, $"{chart}?tab=test-history"));
        }

        var crossmatches = await _crossmatches.ListAsync(x => x.SpecimenId == specimen.Id, ct);
        var allocations = await _allocations.ListAsync(a => a.SpecimenId == specimen.Id, ct);
        var unitIds = crossmatches.Select(x => x.BloodProductId).Concat(allocations.Select(a => a.BloodProductId)).Distinct().ToList();
        var units = unitIds.Count == 0
            ? new Dictionary<long, string>()
            : (await _units.ListAsync(u => unitIds.Contains(u.Id), ct)).ToDictionary(u => u.Id, u => u.UnitNumber);
        string Unit(long id) => units.GetValueOrDefault(id) ?? $"unit {id}";

        foreach (var x in crossmatches)
        {
            events.Add(new(x.PerformedUtc, TimelineCategory.Product,
                x.ClinicalStatus == CrossmatchClinicalStatus.Incompatible ? TimelineSeverity.Critical : TimelineSeverity.Info,
                $"Crossmatch {Unit(x.BloodProductId)}: {x.ClinicalStatus}",
                x.ExpiresUtc is DateTime until ? $"Valid until {until:yyyy-MM-dd HH:mm} UTC" : null,
                x.PerformedBy, $"/inventory/units/{x.BloodProductId}", $"xm-{x.Id}"));
        }

        foreach (var a in allocations)
        {
            events.Add(new(a.AllocatedUtc, TimelineCategory.Product, TimelineSeverity.Info,
                $"Unit {Unit(a.BloodProductId)} reserved on this specimen",
                PatientTimelineService.Join($"Now {a.Status}",
                    a.Status == AllocationStatus.Released && !string.IsNullOrWhiteSpace(a.ReleaseReason) ? $"Released: {a.ReleaseReason}" : null),
                a.AllocatedBy, $"/inventory/units/{a.BloodProductId}", $"alc-{a.Id}"));
        }

        return new SpecimenTimelineDto(specimen.Id, specimen.PatientId, specimen.AccessionNumber, specimen.Status,
            PatientTimelineService.Order(events));
    }

    private static string? IdentifierText(Specimen s)
    {
        var ids = new[] { s.Identifier1Value, s.Identifier2Value }.Where(v => !string.IsNullOrWhiteSpace(v)).ToList();
        return ids.Count == 0 ? null : $"Labeled with {string.Join(" and ", ids)}";
    }

    /// <summary>"Field: old → new" for top-level scalar values that differ. Unreadable JSON is skipped, not guessed.</summary>
    public static string? Changes(string? oldJson, string? newJson)
    {
        var before = Flatten(oldJson);
        var after = Flatten(newJson);
        if (after.Count == 0)
        {
            return null;
        }

        var lines = after
            .Where(kv => !before.TryGetValue(kv.Key, out var old) || old != kv.Value)
            .Select(kv => before.TryGetValue(kv.Key, out var old)
                ? $"{kv.Key}: {Show(old)} → {Show(kv.Value)}"
                : $"{kv.Key}: {Show(kv.Value)}")
            .ToList();
        if (lines.Count == 0)
        {
            return null;
        }

        var shown = string.Join(" · ", lines.Take(MaxChangesShown));
        return lines.Count > MaxChangesShown ? $"{shown} · and {lines.Count - MaxChangesShown} more (see the audit trail)" : shown;
    }

    private static string Show(string? value) => string.IsNullOrEmpty(value) ? "blank" : value;

    private static Dictionary<string, string?> Flatten(string? json)
    {
        var map = new Dictionary<string, string?>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(json))
        {
            return map;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return map;
            }

            foreach (var p in doc.RootElement.EnumerateObject())
            {
                map[p.Name] = p.Value.ValueKind switch
                {
                    JsonValueKind.Null => null,
                    JsonValueKind.String => p.Value.GetString(),
                    JsonValueKind.Object or JsonValueKind.Array => null,
                    _ => p.Value.GetRawText()
                };
            }
        }
        catch (JsonException)
        {
            map.Clear();
        }

        return map;
    }
}
