using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Domain.Audit;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Application.PatientWorkspace;

public sealed record OrderTimelineDto(
    long OrderId,
    long PatientId,
    string OrderNumber,
    OrderStatus Status,
    IReadOnlyList<TimelineEventDto> Events);

/// <summary>
/// Everything that happened to one order, newest first: placed (priority, source, needed-by), each audited change
/// (needed-by, cancel, discontinue, specimen link) with who and which workstation, overrides, its specimens' collection
/// and receipt, results, and for product orders the reservations, crossmatches, issues, returns, and transfusions of
/// its units. Read-only.
/// </summary>
public sealed class OrderTimelineService
{
    private readonly IRepository<Order> _orders;
    private readonly IRepository<OrderSpecimen> _orderSpecimens;
    private readonly IRepository<Specimen> _specimens;
    private readonly IRepository<TestResult> _results;
    private readonly IRepository<Allocation> _allocations;
    private readonly IRepository<Crossmatch> _crossmatches;
    private readonly IRepository<Issue> _issues;
    private readonly IRepository<Return> _returns;
    private readonly IRepository<TransfusionEvent> _transfusions;
    private readonly IRepository<BloodUnit> _units;
    private readonly IRepository<Override> _overrides;
    private readonly IAuditQuery _audit;
    private readonly IClock _clock;

    public OrderTimelineService(
        IRepository<Order> orders,
        IRepository<OrderSpecimen> orderSpecimens,
        IRepository<Specimen> specimens,
        IRepository<TestResult> results,
        IRepository<Allocation> allocations,
        IRepository<Crossmatch> crossmatches,
        IRepository<Issue> issues,
        IRepository<Return> returns,
        IRepository<TransfusionEvent> transfusions,
        IRepository<BloodUnit> units,
        IRepository<Override> overrides,
        IAuditQuery audit,
        IClock clock)
    {
        _orders = orders;
        _orderSpecimens = orderSpecimens;
        _specimens = specimens;
        _results = results;
        _allocations = allocations;
        _crossmatches = crossmatches;
        _issues = issues;
        _returns = returns;
        _transfusions = transfusions;
        _units = units;
        _overrides = overrides;
        _audit = audit;
        _clock = clock;
    }

    /// <summary>The order's history, or null when the order is not this patient's.</summary>
    public async Task<OrderTimelineDto?> GetAsync(long patientId, long orderId, CancellationToken ct = default)
    {
        var order = (await _orders.ListAsync(o => o.Id == orderId && o.PatientId == patientId, ct)).FirstOrDefault();
        if (order is null)
        {
            return null;
        }

        var chart = $"/patients/{patientId}";
        var join = PatientTimelineService.Join;
        var events = new List<TimelineEventDto>
        {
            new(order.OrderedUtc, TimelineCategory.Order, TimelineSeverity.Info,
                $"Ordered: {(string.IsNullOrWhiteSpace(order.OrderName) ? order.TestCode : order.OrderName)}",
                join($"{order.Priority}", $"from {order.Source}",
                    string.IsNullOrWhiteSpace(order.OrderingProvider) ? null : $"provider {order.OrderingProvider}",
                    order.NeededByUtc is DateTime nb ? $"needed by {ClinicalTimeText.Local(nb)}" : null),
                order.OrderedByUser ?? order.CreatedBy, $"{chart}?tab=orders", $"ord-{order.Id}")
        };

        // Only rows the order workflow wrote on purpose; the generic Create/Update rows repeat the whole record.
        foreach (var row in (await _audit.ListForEntityAsync(nameof(Order), order.Id, ct))
                     .Where(a => a.EventType is not (AuditEventType.Create or AuditEventType.Update or AuditEventType.Delete)))
        {
            var reason = string.IsNullOrWhiteSpace(row.Reason) ? "Order changed" : row.Reason.Trim().TrimEnd('.');
            var attention = row.NewValueJson?.Contains("\"Cancel", StringComparison.Ordinal) == true
                || reason.Contains("cancel", StringComparison.OrdinalIgnoreCase)
                || reason.Contains("discontinu", StringComparison.OrdinalIgnoreCase);
            events.Add(new(row.OccurredUtc, TimelineCategory.Order, attention ? TimelineSeverity.Warning : TimelineSeverity.Info,
                reason,
                SpecimenTimelineService.Changes(row.OldValueJson, row.NewValueJson),
                string.IsNullOrWhiteSpace(row.Workstation) ? row.UserName : $"{row.UserName} at {row.Workstation}",
                null, $"aud-{row.Id}"));
        }

        var open = order.Status is not (OrderStatus.Completed or OrderStatus.Cancelled or OrderStatus.Discontinued);
        if (order.NeededByUtc is DateTime neededBy && open)
        {
            var passed = neededBy <= _clock.UtcNow;
            events.Add(new(neededBy, TimelineCategory.Order, passed ? TimelineSeverity.Warning : TimelineSeverity.Info,
                passed ? "Needed-by time passed" : "Needed by",
                passed ? "The order is still open" : "Scheduled; not yet reached",
                null, null, $"ord-{order.Id}-needed"));
        }

        foreach (var o in await _overrides.ListAsync(o => o.ContextType == nameof(Order) && o.ContextId == order.Id, ct))
        {
            events.Add(new(o.OverriddenUtc, TimelineCategory.Alert, TimelineSeverity.Warning,
                $"Override {o.RuleCode}",
                join($"Reason: {o.Reason}", string.IsNullOrWhiteSpace(o.AuthorizedBy) ? null : $"authorized by {o.AuthorizedBy}"),
                o.CreatedBy, null, $"ovr-{o.Id}"));
        }

        var specimenIds = (await _orderSpecimens.ListAsync(l => l.OrderId == order.Id, ct)).Select(l => l.SpecimenId).Distinct().ToList();
        var specimens = specimenIds.Count == 0 ? [] : await _specimens.ListAsync(s => specimenIds.Contains(s.Id), ct);
        var accessionById = specimens.ToDictionary(s => s.Id, s => s.AccessionNumber);
        foreach (var s in specimens)
        {
            var rejected = s.Status == SpecimenStatus.Rejected;
            events.Add(new(s.CollectedUtc, TimelineCategory.Specimen, rejected ? TimelineSeverity.Warning : TimelineSeverity.Info,
                $"Specimen {s.AccessionNumber} collected ({s.SpecimenType})",
                rejected ? $"Rejected{(string.IsNullOrWhiteSpace(s.RejectionReason) ? "" : $": {s.RejectionReason}")}" : $"Now {s.Status}",
                s.Collector, $"{chart}?tab=specimens", $"spc-{s.Id}-collect"));
            if (s.ReceivedUtc is DateTime received)
            {
                events.Add(new(received, TimelineCategory.Specimen, TimelineSeverity.Info,
                    $"Specimen {s.AccessionNumber} received in the blood bank", null, null, $"{chart}?tab=specimens", $"spc-{s.Id}-receive"));
            }
        }

        foreach (var r in await _results.ListAsync(r => r.OrderId == order.Id, ct))
        {
            events.AddRange(PatientTimelineService.ResultEvents(r, accessionById.GetValueOrDefault(r.SpecimenId) ?? $"specimen {r.SpecimenId}", $"{chart}?tab=test-history"));
        }

        var allocations = await _allocations.ListAsync(a => a.OrderId == order.Id, ct);
        var issues = await _issues.ListAsync(i => i.OrderId == order.Id, ct);
        var unitIds = allocations.Select(a => a.BloodProductId).Concat(issues.Select(i => i.BloodProductId)).Distinct().ToList();
        // Crossmatches of this order's units on this patient's specimens for the order (or any, when none is linked).
        var crossmatches = unitIds.Count == 0
            ? []
            : (await _crossmatches.ListAsync(x => x.PatientId == patientId && unitIds.Contains(x.BloodProductId), ct))
                .Where(x => specimenIds.Count == 0 || specimenIds.Contains(x.SpecimenId) || allocations.Any(a => a.SpecimenId == x.SpecimenId))
                .ToList();
        var issueIds = issues.Select(i => i.Id).ToList();
        var returns = issueIds.Count == 0 ? [] : await _returns.ListAsync(r => issueIds.Contains(r.IssueId), ct);
        var transfusions = issueIds.Count == 0 ? [] : await _transfusions.ListAsync(t => issueIds.Contains(t.IssueId), ct);
        var unitNumbers = unitIds.Count == 0
            ? new Dictionary<long, string>()
            : (await _units.ListAsync(u => unitIds.Contains(u.Id), ct)).ToDictionary(u => u.Id, u => u.UnitNumber);
        string Unit(long id) => unitNumbers.GetValueOrDefault(id) ?? $"unit {id}";
        string UnitHref(long id) => $"/inventory/units/{id}";

        foreach (var a in allocations)
        {
            events.Add(new(a.AllocatedUtc, TimelineCategory.Product, TimelineSeverity.Info,
                $"Unit {Unit(a.BloodProductId)} reserved for this order",
                AllocationTimelineNote.Describe(a),
                a.AllocatedBy, UnitHref(a.BloodProductId), $"alc-{a.Id}"));
        }

        foreach (var x in crossmatches)
        {
            var shown = CrossmatchValidityRule.Shown(x.Result, x.ClinicalStatus);
            events.Add(new(x.PerformedUtc, TimelineCategory.Product, shown == CrossmatchClinicalStatus.Incompatible ? TimelineSeverity.Critical : TimelineSeverity.Info,
                $"Crossmatch {Unit(x.BloodProductId)}: {shown}",
                accessionById.TryGetValue(x.SpecimenId, out var accession) ? $"Specimen {accession}" : null,
                x.PerformedBy, UnitHref(x.BloodProductId), $"xm-{x.Id}"));
        }

        foreach (var i in issues)
        {
            var emergency = i.IssueType is IssueType.EmergencyRelease or IssueType.MassiveTransfusion || i.OverrideId is not null;
            events.Add(new(i.IssuedUtc, TimelineCategory.Product, emergency ? TimelineSeverity.Warning : TimelineSeverity.Info,
                $"Unit {Unit(i.BloodProductId)} issued{(i.IssueType == IssueType.Standard ? "" : $" ({i.IssueType})")}",
                join(string.IsNullOrWhiteSpace(i.IssuedToLocation) ? null : $"To {i.IssuedToLocation}", $"Crossmatch at issue: {i.CrossmatchStatus}"),
                i.IssuedBy, UnitHref(i.BloodProductId), $"iss-{i.Id}"));
        }

        foreach (var r in returns)
        {
            events.Add(new(r.ReturnedUtc, TimelineCategory.Product, TimelineSeverity.Info,
                $"Unit {Unit(r.BloodProductId)} returned",
                join($"Reason: {r.Reason}", r.ReissueEligible ? "eligible to reissue" : "not eligible to reissue"),
                r.ReturnedBy, UnitHref(r.BloodProductId), $"ret-{r.Id}"));
        }

        foreach (var t in transfusions)
        {
            var done = t.StopUtc is not null;
            events.Add(new(t.StartUtc ?? t.StopUtc ?? t.CreatedUtc, TimelineCategory.Transfusion,
                t.ReactionSuspected ? TimelineSeverity.Critical : TimelineSeverity.Info,
                done ? $"Unit {Unit(t.BloodProductId)} transfused" : $"Unit {Unit(t.BloodProductId)} transfusion started",
                join(t.VolumeTransfused is decimal v ? $"{v:0.#} mL" : null, t.ReactionSuspected ? "Reaction suspected" : null),
                t.Transfusionist ?? t.DocumentedBy, UnitHref(t.BloodProductId), $"trf-{t.Id}"));
        }

        return new OrderTimelineDto(order.Id, order.PatientId, order.OrderNumber, order.Status, PatientTimelineService.Order(events));
    }
}
