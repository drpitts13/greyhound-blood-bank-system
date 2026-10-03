using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.ValueObjects;

namespace BloodBankLIS.Application.PatientWorkspace;

/// <summary>
/// Read-only unified product history from allocation, crossmatch, issue, return, and transfusion tables.
/// </summary>
public sealed class PatientProductHistoryService
{
    private readonly IRepository<Allocation> _allocations;
    private readonly IRepository<Crossmatch> _crossmatches;
    private readonly IRepository<Issue> _issues;
    private readonly IRepository<Return> _returns;
    private readonly IRepository<TransfusionEvent> _transfusions;
    private readonly IRepository<BloodUnit> _units;
    private readonly IRepository<ProductType> _productTypes;
    private readonly IRepository<Encounter> _encounters;
    private readonly IRepository<Order> _orders;
    private readonly IRepository<Specimen> _specimens;
    private readonly IRepository<PatientBloodTypeHistory> _bloodTypes;
    private readonly IRepository<Override> _overrides;

    public PatientProductHistoryService(
        IRepository<Allocation> allocations,
        IRepository<Crossmatch> crossmatches,
        IRepository<Issue> issues,
        IRepository<Return> returns,
        IRepository<TransfusionEvent> transfusions,
        IRepository<BloodUnit> units,
        IRepository<ProductType> productTypes,
        IRepository<Encounter> encounters,
        IRepository<Order> orders,
        IRepository<Specimen> specimens,
        IRepository<PatientBloodTypeHistory> bloodTypes,
        IRepository<Override> overrides)
    {
        _allocations = allocations;
        _crossmatches = crossmatches;
        _issues = issues;
        _returns = returns;
        _transfusions = transfusions;
        _units = units;
        _productTypes = productTypes;
        _encounters = encounters;
        _orders = orders;
        _specimens = specimens;
        _bloodTypes = bloodTypes;
        _overrides = overrides;
    }

    public async Task<IReadOnlyList<PatientProductHistoryRowDto>> ListByPatientAsync(
        long patientId,
        long? encounterId = null,
        CancellationToken ct = default)
    {
        var units = (await _units.ListAsync(ct)).ToDictionary(u => u.Id);
        var productTypes = (await _productTypes.ListAsync(ct)).ToDictionary(p => p.Id);
        var encounters = (await _encounters.ListAsync(e => e.PatientId == patientId, ct)).ToDictionary(e => e.Id);
        var orders = (await _orders.ListAsync(o => o.PatientId == patientId, ct)).ToDictionary(o => o.Id);
        var specimens = (await _specimens.ListAsync(s => s.PatientId == patientId, ct)).ToDictionary(s => s.Id);

        var currentType = (await _bloodTypes.ListAsync(h => h.PatientId == patientId && h.IsCurrent, ct))
            .FirstOrDefault();
        var patientBloodType = currentType is null
            ? null
            : FormatBloodType(currentType.Abo, currentType.RhD);

        var crossmatches = await _crossmatches.ListAsync(x => x.PatientId == patientId, ct);
        var latestCrossmatchByUnit = LatestRecordedCrossmatchByUnit(crossmatches);
        var allocations = await _allocations.ListAsync(x => x.PatientId == patientId, ct);
        var allocationsById = allocations.ToDictionary(a => a.Id);
        var rows = new List<PatientProductHistoryRowDto>();

        foreach (var a in allocations)
        {
            if (encounterId.HasValue && a.EncounterId != encounterId)
            {
                continue;
            }

            var unit = units.GetValueOrDefault(a.BloodProductId);
            var (visit, orderNum, missing) = ResolveContext(a.EncounterId, a.OrderId, encounters, orders);
            rows.Add(new PatientProductHistoryRowDto(
                a.Id, nameof(Allocation),
                a.Status == AllocationStatus.Reserved ? PatientProductHistoryEventType.Assigned : PatientProductHistoryEventType.Allocated,
                a.AllocatedUtc, unit?.UnitNumber, GetProductName(unit, productTypes), FormatBloodType(unit?.Abo, unit?.RhD),
                patientBloodType, visit, orderNum, ResolveAccession(a.SpecimenId, specimens),
                LatestCrossmatchStatus(latestCrossmatchByUnit, unit?.Id), Person(a.AllocatedBy), null, null, null, null, null, null,
                false, missing, a.Status == AllocationStatus.Reserved,
                UnitId: unit?.Id, UnitExpiresUtc: unit?.ExpiresUtc));
        }

        foreach (var c in crossmatches)
        {
            var unit = units.GetValueOrDefault(c.BloodProductId);
            rows.Add(new PatientProductHistoryRowDto(
                c.Id, nameof(Crossmatch), PatientProductHistoryEventType.Crossmatched, c.PerformedUtc,
                unit?.UnitNumber, GetProductName(unit, productTypes), FormatBloodType(unit?.Abo, unit?.RhD),
                patientBloodType, null, null, specimens.GetValueOrDefault(c.SpecimenId)?.AccessionNumber,
                RecordedCrossmatchStatus(c.Result), Person(c.PerformedBy), null, null, null, null, null, null, false, true, false,
                UnitId: unit?.Id, UnitExpiresUtc: unit?.ExpiresUtc));
        }

        var issues = await _issues.ListAsync(x => x.PatientId == patientId, ct);
        var authorizers = await LoadEmergencyReleaseAuthorizersAsync(issues, ct);
        var issueIds = issues.Select(i => i.Id).ToHashSet();
        var returnsByIssue = (await _returns.ListAsync(ct))
            .Where(r => issueIds.Contains(r.IssueId))
            .GroupBy(r => r.IssueId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.ReturnedUtc).First());

        var transfusionsByIssue = (await _transfusions.ListAsync(t => t.PatientId == patientId, ct))
            .GroupBy(t => t.IssueId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(t => t.CreatedUtc).First());

        foreach (var i in issues)
        {
            if (encounterId.HasValue && i.EncounterId != encounterId)
            {
                continue;
            }

            var unit = units.GetValueOrDefault(i.BloodProductId);
            var (visit, orderNum, missing) = ResolveContext(i.EncounterId, i.OrderId, encounters, orders);
            var hasReturn = returnsByIssue.ContainsKey(i.Id);
            var hasTransfusion = transfusionsByIssue.ContainsKey(i.Id);
            var isOpen = i.Status == IssueStatus.Issued && !hasReturn && !hasTransfusion;

            rows.Add(new PatientProductHistoryRowDto(
                i.Id, nameof(Issue), PatientProductHistoryEventType.Issued, i.IssuedUtc,
                unit?.UnitNumber, GetProductName(unit, productTypes), FormatBloodType(unit?.Abo, unit?.RhD),
                patientBloodType, visit, orderNum, LinkedSpecimenAccession(i, allocationsById, specimens), LatestCrossmatchStatus(latestCrossmatchByUnit, unit?.Id), Person(i.IssuedBy), i.IssuedToLocation,
                null, null, null, null, i.Status.ToString(), false, missing, isOpen,
                UnitId: unit?.Id, IssueId: i.Id, UnitExpiresUtc: unit?.ExpiresUtc, IssuedTo: Person(i.IssuedTo), CoolerId: Person(i.CoolerId), IssueType: i.IssueType.ToString(), EmergencyReleaseReason: Person(i.EmergencyReleaseDetails), SecondVerifier: Person(i.SecondVerifier), EmergencyReleaseAuthorizer: Authorizer(i, authorizers), IssueAppearance: i.IssueAppearance.ToString(), WardAppearance: WardReceiptAppearance(i), PtagCode: Person(i.PtagCode), IssueCrossmatchStatus: i.CrossmatchStatus.ToString()));
        }

        foreach (var r in await _returns.ListAsync(ct))
        {
            if (!issueIds.Contains(r.IssueId))
            {
                continue;
            }

            var issue = issues.First(x => x.Id == r.IssueId);
            if (encounterId.HasValue && issue.EncounterId != encounterId)
            {
                continue;
            }

            var unit = units.GetValueOrDefault(r.BloodProductId);
            var (visit, orderNum, missing) = ResolveContext(issue.EncounterId, issue.OrderId, encounters, orders);
            rows.Add(new PatientProductHistoryRowDto(
                r.Id, nameof(Return), PatientProductHistoryEventType.Returned, r.ReturnedUtc,
                unit?.UnitNumber, GetProductName(unit, productTypes), FormatBloodType(unit?.Abo, unit?.RhD),
                patientBloodType, visit, orderNum, LinkedSpecimenAccession(issue, allocationsById, specimens), LatestCrossmatchStatus(latestCrossmatchByUnit, unit?.Id), Person(r.ReturnedBy), null, r.ReturnedBy,
                null, null, null, null, false, missing, false,
                UnitId: unit?.Id, IssueId: r.IssueId, UnitExpiresUtc: unit?.ExpiresUtc,
                ReturnReason: Person(r.Reason), ReissueEligible: r.ReissueEligible, IssuedTo: Person(issue.IssuedTo), CoolerId: Person(issue.CoolerId), IssueType: issue.IssueType.ToString(), EmergencyReleaseReason: Person(issue.EmergencyReleaseDetails), SecondVerifier: Person(issue.SecondVerifier), EmergencyReleaseAuthorizer: Authorizer(issue, authorizers), IssueAppearance: issue.IssueAppearance.ToString(), WardAppearance: WardReceiptAppearance(issue), PtagCode: Person(issue.PtagCode), IssueCrossmatchStatus: issue.CrossmatchStatus.ToString()));
        }

        foreach (var t in await _transfusions.ListAsync(x => x.PatientId == patientId, ct))
        {
            var issue = issues.FirstOrDefault(x => x.Id == t.IssueId);
            if (issue is null)
            {
                continue;
            }

            if (encounterId.HasValue && issue.EncounterId != encounterId)
            {
                continue;
            }

            var unit = units.GetValueOrDefault(t.BloodProductId);
            var (visit, orderNum, missing) = ResolveContext(issue.EncounterId, issue.OrderId, encounters, orders);
            var eventType = t.FinalDisposition == TransfusionDisposition.Stopped
                ? PatientProductHistoryEventType.PartiallyTransfused
                : PatientProductHistoryEventType.Transfused;

            rows.Add(new PatientProductHistoryRowDto(
                t.Id, nameof(TransfusionEvent), eventType, t.StartUtc ?? t.CreatedUtc,
                unit?.UnitNumber, GetProductName(unit, productTypes), FormatBloodType(unit?.Abo, unit?.RhD),
                patientBloodType, visit, orderNum, LinkedSpecimenAccession(issue, allocationsById, specimens), LatestCrossmatchStatus(latestCrossmatchByUnit, unit?.Id), Person(t.DocumentedBy),
                t.Location ?? issue.IssuedToLocation,
                null, t.StartUtc, t.StopUtc, t.VolumeTransfused, t.FinalDisposition.ToString(),
                t.ReactionSuspected, missing, false, t.PatientIdentificationMethod,
                t.Transfusionist, unit?.Id, t.IssueId, unit?.ExpiresUtc, IssuedTo: Person(issue.IssuedTo), CoolerId: Person(issue.CoolerId), IssueType: issue.IssueType.ToString(), EmergencyReleaseReason: Person(issue.EmergencyReleaseDetails), SecondVerifier: Person(issue.SecondVerifier), EmergencyReleaseAuthorizer: Authorizer(issue, authorizers), IssueAppearance: issue.IssueAppearance.ToString(), WardAppearance: WardReceiptAppearance(issue), PtagCode: Person(issue.PtagCode), IssueCrossmatchStatus: issue.CrossmatchStatus.ToString()));
        }

        return rows.OrderByDescending(r => r.EventUtc).ToList();
    }

    private static Dictionary<long, CrossmatchResult> LatestRecordedCrossmatchByUnit(IReadOnlyList<Crossmatch> crossmatches)
    {
        var results = new Dictionary<long, CrossmatchResult>();
        foreach (var group in crossmatches.GroupBy(x => x.BloodProductId))
        {
            var latest = group
                .OrderByDescending(x => x.PerformedUtc)
                .ThenByDescending(x => x.Id)
                .First();
            if (latest.Result is CrossmatchResult.Compatible or CrossmatchResult.Incompatible)
            {
                results[group.Key] = latest.Result;
            }
        }

        return results;
    }

    private static string? LatestCrossmatchStatus(
        IReadOnlyDictionary<long, CrossmatchResult> latestByUnit,
        long? unitId) =>
        unitId is long id && latestByUnit.TryGetValue(id, out var result) ? result.ToString() : null;

    private static string? RecordedCrossmatchStatus(CrossmatchResult result) =>
        result is CrossmatchResult.Compatible or CrossmatchResult.Incompatible ? result.ToString() : null;

    private static (string? VisitNumber, string? OrderNumber, bool MissingContext) ResolveContext(
        long? encounterId,
        long? orderId,
        IReadOnlyDictionary<long, Encounter> encounters,
        IReadOnlyDictionary<long, Order> orders)
    {
        string? visit = null;
        string? orderNum = null;
        var missing = false;

        if (encounterId.HasValue && encounters.TryGetValue(encounterId.Value, out var enc))
        {
            visit = enc.VisitNumber;
        }
        else if (encounterId.HasValue)
        {
            missing = true;
        }

        if (orderId.HasValue && orders.TryGetValue(orderId.Value, out var ord))
        {
            orderNum = ord.OrderNumber;
        }
        else if (orderId.HasValue)
        {
            missing = true;
        }

        if (!encounterId.HasValue && !orderId.HasValue)
        {
            missing = true;
        }

        return (visit, orderNum, missing);
    }

    private static string? LinkedSpecimenAccession(
        Issue issue,
        IReadOnlyDictionary<long, Allocation> allocations,
        IReadOnlyDictionary<long, Specimen> specimens) =>
        issue.AllocationId is long allocationId && allocations.TryGetValue(allocationId, out var allocation)
            ? ResolveAccession(allocation.SpecimenId, specimens)
            : null;

    private async Task<Dictionary<long, string?>> LoadEmergencyReleaseAuthorizersAsync(
        IReadOnlyList<Issue> issues,
        CancellationToken ct)
    {
        var overrideIds = issues.Where(i => i.OverrideId is long).Select(i => i.OverrideId!.Value).Distinct().ToList();
        if (overrideIds.Count == 0)
        {
            return [];
        }

        var overrides = await _overrides.ListAsync(o => overrideIds.Contains(o.Id), ct);
        return overrides.ToDictionary(o => o.Id, o => Person(o.AuthorizedBy));
    }

    private static string? Authorizer(Issue issue, IReadOnlyDictionary<long, string?> authorizers) =>
        issue.OverrideId is long id && authorizers.TryGetValue(id, out var name) ? name : null;

    private static string? WardReceiptAppearance(Issue issue) =>
        issue.WardReceivedUtc is null ? null : issue.WardAppearance.ToString();

    private static string? Person(string? name) =>
        string.IsNullOrWhiteSpace(name) ? null : name.Trim();

    private static string? ResolveAccession(long? specimenId, IReadOnlyDictionary<long, Specimen> specimens) =>
        specimenId.HasValue && specimens.TryGetValue(specimenId.Value, out var sp) ? sp.AccessionNumber : null;

    private static string? GetProductName(BloodUnit? unit, IReadOnlyDictionary<long, ProductType> productTypes) =>
        unit is not null && productTypes.TryGetValue(unit.ProductTypeId, out var pt) ? pt.Name : null;

    private static string? FormatBloodType(AboGroup? abo, RhType? rh)
    {
        if (abo is null || rh is null || abo == AboGroup.Unknown)
        {
            return null;
        }

        var rhLabel = rh == RhType.Positive ? "+" : rh == RhType.Negative ? "-" : "";
        return $"{abo}{rhLabel}";
    }
}
