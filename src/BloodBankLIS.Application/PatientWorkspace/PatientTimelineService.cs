using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Application.PatientWorkspace;

public enum TimelineCategory
{
    Visit = 0,
    Order = 1,
    Specimen = 2,
    Testing = 3,
    Alert = 4,
    Product = 5,
    Transfusion = 6,
    Rhig = 7
}

/// <summary>Informational, Warning (needs attention or was changed), Critical (safety-relevant: antibody, incompatibility, reaction).</summary>
public enum TimelineSeverity
{
    Info = 0,
    Warning = 1,
    Critical = 2
}

public sealed record TimelineEventDto(
    DateTime AtUtc,
    TimelineCategory Category,
    TimelineSeverity Severity,
    string Title,
    string? Detail,
    string? By,
    string? Href,
    string SourceKey);

/// <summary>
/// One chronological, read-only view of what happened to a patient, built from the stored records.
/// Each event uses the time the record itself holds; no time is inferred from a later edit. A change
/// without its own timestamp (an order cancellation, a specimen rejection) is reported on the event it changed.
/// </summary>
public sealed class PatientTimelineService
{
    private readonly IRepository<Encounter> _encounters;
    private readonly IRepository<Order> _orders;
    private readonly IRepository<Specimen> _specimens;
    private readonly IRepository<TestResult> _results;
    private readonly IRepository<PatientBloodTypeHistory> _bloodTypes;
    private readonly IRepository<AntibodyHistory> _antibodies;
    private readonly IRepository<SpecialTransfusionRequirement> _requirements;
    private readonly IRepository<SpecialRequirementDefinition> _requirementDefinitions;
    private readonly IRepository<Allocation> _allocations;
    private readonly IRepository<Crossmatch> _crossmatches;
    private readonly IRepository<Issue> _issues;
    private readonly IRepository<Return> _returns;
    private readonly IRepository<TransfusionEvent> _transfusions;
    private readonly IRepository<ReactionInvestigation> _reactions;
    private readonly IRepository<BloodUnit> _units;
    private readonly IRepository<Override> _overrides;
    private readonly IRepository<RhigEvaluation>? _rhig;

    public PatientTimelineService(
        IRepository<Encounter> encounters,
        IRepository<Order> orders,
        IRepository<Specimen> specimens,
        IRepository<TestResult> results,
        IRepository<PatientBloodTypeHistory> bloodTypes,
        IRepository<AntibodyHistory> antibodies,
        IRepository<SpecialTransfusionRequirement> requirements,
        IRepository<SpecialRequirementDefinition> requirementDefinitions,
        IRepository<Allocation> allocations,
        IRepository<Crossmatch> crossmatches,
        IRepository<Issue> issues,
        IRepository<Return> returns,
        IRepository<TransfusionEvent> transfusions,
        IRepository<ReactionInvestigation> reactions,
        IRepository<BloodUnit> units,
        IRepository<Override> overrides,
        IRepository<RhigEvaluation>? rhig = null)
    {
        _rhig = rhig;
        _encounters = encounters;
        _orders = orders;
        _specimens = specimens;
        _results = results;
        _bloodTypes = bloodTypes;
        _antibodies = antibodies;
        _requirements = requirements;
        _requirementDefinitions = requirementDefinitions;
        _allocations = allocations;
        _crossmatches = crossmatches;
        _issues = issues;
        _returns = returns;
        _transfusions = transfusions;
        _reactions = reactions;
        _units = units;
        _overrides = overrides;
    }

    /// <summary>Newest first. Ties keep a stable order by category and source.</summary>
    public async Task<IReadOnlyList<TimelineEventDto>> GetAsync(long patientId, CancellationToken ct = default)
    {
        var events = new List<TimelineEventDto>();
        var chart = $"/patients/{patientId}";

        foreach (var e in await _encounters.ListAsync(x => x.PatientId == patientId, ct))
        {
            var where = string.IsNullOrWhiteSpace(e.CurrentLocation) ? null : e.CurrentLocation;
            if (e.AdmitUtc is DateTime admit)
            {
                events.Add(new(admit, TimelineCategory.Visit, TimelineSeverity.Info,
                    $"Visit {e.VisitNumber} started ({e.EncounterType})", where, e.AttendingProvider, $"{chart}?tab=visits", $"enc-{e.Id}-admit"));
            }

            if (e.DischargeUtc is DateTime discharge)
            {
                events.Add(new(discharge, TimelineCategory.Visit, TimelineSeverity.Info,
                    $"Visit {e.VisitNumber} ended", e.DischargeDisposition, null, $"{chart}?tab=visits", $"enc-{e.Id}-discharge"));
            }
        }

        foreach (var o in await _orders.ListAsync(x => x.PatientId == patientId, ct))
        {
            var cancelled = o.Status == OrderStatus.Cancelled;
            var detail = Join(
                $"{o.OrderNumber} · {o.Priority}",
                cancelled ? $"Cancelled{(string.IsNullOrWhiteSpace(o.CancellationReason) ? "" : $": {o.CancellationReason}")}" : $"Now {o.Status}");
            events.Add(new(o.OrderedUtc, TimelineCategory.Order, cancelled ? TimelineSeverity.Warning : TimelineSeverity.Info,
                $"{(o.OrderCategory == OrderCategory.Product ? "Product order" : "Test order")}: {Name(o.OrderName, o.TestCode)}",
                detail, o.OrderingProvider ?? o.CreatedBy, $"{chart}?tab=orders", $"ord-{o.Id}"));
        }

        var specimens = await _specimens.ListAsync(x => x.PatientId == patientId, ct);
        var accessionById = specimens.ToDictionary(s => s.Id, s => s.AccessionNumber);
        foreach (var s in specimens)
        {
            var rejected = s.Status == SpecimenStatus.Rejected;
            events.Add(new(s.CollectedUtc, TimelineCategory.Specimen, rejected ? TimelineSeverity.Warning : TimelineSeverity.Info,
                $"Specimen {s.AccessionNumber} collected ({s.SpecimenType})",
                Join(rejected ? $"Rejected{(string.IsNullOrWhiteSpace(s.RejectionReason) ? "" : $": {s.RejectionReason}")}" : $"Now {s.Status}",
                    s.ExpiresUtc is DateTime exp ? $"expires {exp:yyyy-MM-dd HH:mm} UTC" : null,
                    // Cycle 680: an infant's specimen valid for the admission (OCD-074).
                    s.NeonatalStandardExpiresUtc is null ? null : "valid for the admission (infant)"),
                s.Collector, $"{chart}?tab=specimens", $"spc-{s.Id}-collect"));
            if (s.ReceivedUtc is DateTime received)
            {
                events.Add(new(received, TimelineCategory.Specimen, TimelineSeverity.Info,
                    $"Specimen {s.AccessionNumber} received in the lab", null, null, $"{chart}?tab=specimens", $"spc-{s.Id}-receive"));
            }
        }

        foreach (var r in await _results.ListAsync(x => x.PatientId == patientId, ct))
        {
            var accession = accessionById.GetValueOrDefault(r.SpecimenId) ?? $"specimen {r.SpecimenId}";
            events.AddRange(ResultEvents(r, accession, $"{chart}?tab=test-history"));
        }

        foreach (var b in await _bloodTypes.ListAsync(x => x.PatientId == patientId, ct))
        {
            events.Add(new(b.CreatedUtc, TimelineCategory.Testing, TimelineSeverity.Info,
                $"Blood type recorded: {b.Abo} {b.RhD}",
                Join($"Source: {b.Source}", b.IsCurrent ? "current" : "superseded", b.Reason),
                b.CreatedBy, chart, $"abo-{b.Id}"));
        }

        foreach (var a in await _antibodies.ListAsync(x => x.PatientId == patientId, ct))
        {
            events.Add(new(a.CreatedUtc, TimelineCategory.Alert, TimelineSeverity.Critical,
                $"Antibody recorded: {a.AntibodySpecificity} ({a.Status})",
                Join(OutsideAntibodyReportRule.SourceLabel(a.ReportingFacility, a.IdentifiedOn), a.Comment,
                    a.IsActive ? null : $"Inactivated{(string.IsNullOrWhiteSpace(a.DeactivationReason) ? "" : $": {a.DeactivationReason}")}"),
                a.CreatedBy, chart, $"abs-{a.Id}"));
            // Cycle 680: when this laboratory confirmed an outside report (OCD-073), and how.
            if (a.ConfirmedUtc is DateTime confirmed)
            {
                events.Add(new(confirmed, TimelineCategory.Alert, TimelineSeverity.Info,
                    $"Outside antibody confirmed here: {a.AntibodySpecificity}",
                    a.ConfirmationNote, a.ConfirmedBy, chart, $"abs-{a.Id}-confirm"));
            }
        }

        var requirements = await _requirements.ListAsync(x => x.PatientId == patientId, ct);
        var definitionIds = requirements.Select(r => r.RequirementDefinitionId).OfType<long>().Distinct().ToList();
        var definitions = definitionIds.Count == 0
            ? new Dictionary<long, SpecialRequirementDefinition>()
            : (await _requirementDefinitions.ListAsync(d => definitionIds.Contains(d.Id), ct)).ToDictionary(d => d.Id);
        foreach (var r in requirements)
        {
            var name = SpecialRequirementLabel.Display(
                r.RequirementDefinitionId is long id && definitions.TryGetValue(id, out var d) ? d.Name : null, r.RequirementType, r.AntigenCode);
            events.Add(new(r.EffectiveUtc, TimelineCategory.Alert, TimelineSeverity.Warning,
                $"Transfusion requirement added: {name}",
                Join($"Reason: {r.Reason}", r.ExpiresUtc is DateTime until ? $"until {until:yyyy-MM-dd}" : null,
                    r.IsActive ? null : $"Inactivated{(string.IsNullOrWhiteSpace(r.DeactivationReason) ? "" : $": {r.DeactivationReason}")}"),
                r.EnteredBy, chart, $"req-{r.Id}"));
        }

        var allocations = await _allocations.ListAsync(x => x.PatientId == patientId, ct);
        var crossmatches = await _crossmatches.ListAsync(x => x.PatientId == patientId, ct);
        var issues = await _issues.ListAsync(x => x.PatientId == patientId, ct);
        var issueIds = issues.Select(i => i.Id).ToList();
        var returns = issueIds.Count == 0 ? [] : await _returns.ListAsync(x => issueIds.Contains(x.IssueId), ct);
        var transfusions = await _transfusions.ListAsync(x => x.PatientId == patientId, ct);
        var reactions = await _reactions.ListAsync(x => x.PatientId == patientId, ct);

        var unitIds = allocations.Select(a => a.BloodProductId)
            .Concat(crossmatches.Select(x => x.BloodProductId))
            .Concat(issues.Select(i => i.BloodProductId))
            .Concat(returns.Select(r => r.BloodProductId))
            .Concat(transfusions.Select(t => t.BloodProductId))
            .Concat(reactions.Select(r => r.BloodProductId))
            .Distinct().ToList();
        var unitNumbers = unitIds.Count == 0
            ? new Dictionary<long, string>()
            : (await _units.ListAsync(u => unitIds.Contains(u.Id), ct)).ToDictionary(u => u.Id, u => u.UnitNumber);
        string Unit(long id) => unitNumbers.GetValueOrDefault(id) ?? $"unit {id}";
        string UnitHref(long id) => $"/inventory/units/{id}";

        foreach (var a in allocations)
        {
            events.Add(new(a.AllocatedUtc, TimelineCategory.Product, TimelineSeverity.Info,
                $"Unit {Unit(a.BloodProductId)} reserved",
                AllocationTimelineNote.Describe(a),
                a.AllocatedBy, UnitHref(a.BloodProductId), $"alc-{a.Id}"));
        }

        foreach (var x in crossmatches)
        {
            var shown = CrossmatchValidityRule.Shown(x.Result, x.ClinicalStatus);
            var incompatible = shown == CrossmatchClinicalStatus.Incompatible;
            events.Add(new(x.PerformedUtc, TimelineCategory.Product, incompatible ? TimelineSeverity.Critical : TimelineSeverity.Info,
                $"Crossmatch {Unit(x.BloodProductId)}: {shown}",
                $"Specimen {accessionById.GetValueOrDefault(x.SpecimenId) ?? x.SpecimenId.ToString()}",
                x.PerformedBy, UnitHref(x.BloodProductId), $"xm-{x.Id}"));
        }

        var overrideIds = issues.Select(i => i.OverrideId).OfType<long>().Distinct().ToList();
        var overrides = overrideIds.Count == 0
            ? new Dictionary<long, Override>()
            : (await _overrides.ListAsync(o => overrideIds.Contains(o.Id), ct)).ToDictionary(o => o.Id);
        foreach (var i in issues)
        {
            var emergency = i.IssueType is IssueType.EmergencyRelease or IssueType.MassiveTransfusion || i.OverrideId is not null;
            events.Add(new(i.IssuedUtc, TimelineCategory.Product, emergency ? TimelineSeverity.Warning : TimelineSeverity.Info,
                $"Unit {Unit(i.BloodProductId)} issued{(i.IssueType == IssueType.Standard ? "" : $" ({i.IssueType})")}",
                Join(string.IsNullOrWhiteSpace(i.IssuedToLocation) ? null : $"To {i.IssuedToLocation}",
                    $"Crossmatch at issue: {i.CrossmatchStatus}",
                    OverrideText(i.OverrideId, overrides),
                    // Cycle 680: how the issue was entered, as Product History says it (Cycle 676).
                    IssueEntryNotes.For(i.LateEntryReason, i.DowntimeBackEntry, i.OutsideOutageReason, i.NeonatalNoCrossmatch) is { Count: > 0 } notes
                        ? string.Join("; ", notes)
                        : null),
                i.IssuedBy, UnitHref(i.BloodProductId), $"iss-{i.Id}"));
        }

        foreach (var i in issues.Where(i => i.WardReceivedUtc is not null))
        {
            events.Add(new(i.WardReceivedUtc!.Value, TimelineCategory.Product, TimelineSeverity.Info,
                $"Unit {Unit(i.BloodProductId)} received on the ward",
                Join(string.IsNullOrWhiteSpace(i.IssuedToLocation) ? null : $"At {i.IssuedToLocation}",
                    i.WardAppearance == UnitAppearance.Acceptable ? null : $"Appearance: {i.WardAppearance}"),
                i.WardReceivedBy, UnitHref(i.BloodProductId), $"iss-{i.Id}-ward"));
        }

        foreach (var r in returns)
        {
            events.Add(new(r.ReturnedUtc, TimelineCategory.Product, TimelineSeverity.Info,
                $"Unit {Unit(r.BloodProductId)} returned",
                Join($"Reason: {r.Reason}", r.ReissueEligible ? "eligible to reissue" : "not eligible to reissue"),
                r.ReturnedBy, UnitHref(r.BloodProductId), $"ret-{r.Id}"));
        }

        var openIssueIds = issues.Where(i => i.Status == IssueStatus.Issued).Select(i => i.Id).ToHashSet();
        foreach (var t in transfusions)
        {
            var at = t.StartUtc ?? t.StopUtc ?? t.CreatedUtc;
            if (t.StopUtc is null && openIssueIds.Contains(t.IssueId))
            {
                events.Add(new(at, TimelineCategory.Transfusion, TimelineSeverity.Warning,
                    $"Unit {Unit(t.BloodProductId)} transfusion started",
                    "In progress, not yet completed",
                    t.Transfusionist ?? t.DocumentedBy, UnitHref(t.BloodProductId), $"trf-{t.Id}"));
                continue;
            }

            events.Add(new(at, TimelineCategory.Transfusion, t.ReactionSuspected ? TimelineSeverity.Critical : TimelineSeverity.Info,
                $"Unit {Unit(t.BloodProductId)} transfused",
                Join(t.StopUtc is DateTime stop && t.StartUtc is not null ? $"stopped {stop:yyyy-MM-dd HH:mm} UTC" : null,
                    t.VolumeTransfused is decimal v ? $"{v:0.#} mL" : null,
                    t.ReactionSuspected ? "Reaction suspected" : null),
                t.Transfusionist ?? t.DocumentedBy, UnitHref(t.BloodProductId), $"trf-{t.Id}"));
        }

        foreach (var r in reactions)
        {
            events.Add(new(r.ReportedUtc, TimelineCategory.Transfusion, TimelineSeverity.Critical,
                $"Transfusion reaction reported: unit {Unit(r.BloodProductId)}",
                $"Investigation {r.Status}", r.ReportedBy, "/reactions", $"rxn-{r.Id}"));
            if (r.ClosedUtc is DateTime closed)
            {
                events.Add(new(closed, TimelineCategory.Transfusion, TimelineSeverity.Info,
                    $"Reaction investigation closed: unit {Unit(r.BloodProductId)}", null, r.ClosedBy, "/reactions", $"rxn-{r.Id}-close"));
            }
        }

        if (_rhig is not null)
        {
            foreach (var e in await _rhig.ListAsync(x => x.PatientId == patientId, ct))
            {
                events.AddRange(RhigEvents(e));
            }
        }

        return Order(events);
    }

    /// <summary>An RhIG evaluation on the timeline: recorded, completed, and the RhIG given (lot and vials).</summary>
    internal static IEnumerable<TimelineEventDto> RhigEvents(RhigEvaluation e)
    {
        var href = $"/patients/{e.PatientId}?tab=rhig";
        yield return new(e.RecordedUtc, TimelineCategory.Rhig, TimelineSeverity.Info,
            "RhIG evaluation recorded", $"Event {ClinicalTimeText.Local(e.EventUtc)}", e.RecordedBy, href, $"rhig-{e.Id}");
        if (e.CompletedUtc is DateTime completed)
        {
            var decision = e.Decision switch
            {
                RhigDecision.Candidate => e.VialsRecommended is int v ? $"Candidate, {v} vial{(v == 1 ? "" : "s")} recommended" : "Candidate",
                RhigDecision.NotCandidate => "Not a candidate",
                _ => "Not decided"
            };
            yield return new(completed, TimelineCategory.Rhig, TimelineSeverity.Info,
                "RhIG evaluation completed", decision, e.CompletedBy, href, $"rhig-{e.Id}-complete");
        }

        if (e.GivenUtc is DateTime given)
        {
            var difference = RhigEvaluationRule.GivenDifference(e.VialsRecommended, e.GivenVials);
            yield return new(given, TimelineCategory.Rhig, difference is null ? TimelineSeverity.Info : TimelineSeverity.Warning,
                $"RhIG given: {e.GivenVials} vial{(e.GivenVials == 1 ? "" : "s")}, lot {e.GivenLot}", difference, e.GivenBy, href, $"rhig-{e.Id}-given");
        }
    }

    /// <summary>Entered, verified, corrected, and invalidated events for one result. Shared with the specimen timeline.</summary>
    internal static IEnumerable<TimelineEventDto> ResultEvents(TestResult r, string accession, string href)
    {
        var value = string.IsNullOrWhiteSpace(r.Interpretation) ? r.Value : r.Interpretation;
        if (r.EnteredUtc is DateTime entered)
        {
            var corrected = !string.IsNullOrWhiteSpace(r.CorrectionReason);
            yield return new(entered, TimelineCategory.Testing, corrected ? TimelineSeverity.Warning : TimelineSeverity.Info,
                corrected ? $"{r.TestCode} corrected: {value}" : $"{r.TestCode} resulted: {value}",
                Join($"Specimen {accession}", corrected ? $"Reason: {r.CorrectionReason}" : null,
                    r.Status == ResultStatus.Corrected ? "Later replaced by a correction" : null),
                r.EnteredBy, href, $"res-{r.Id}-enter");
        }

        if (r.VerifiedUtc is DateTime verified)
        {
            yield return new(verified, TimelineCategory.Testing, TimelineSeverity.Info,
                $"{r.TestCode} verified", $"Specimen {accession}", r.VerifiedBy, href, $"res-{r.Id}-verify");
        }

        if (r.InvalidatedUtc is DateTime invalidated)
        {
            yield return new(invalidated, TimelineCategory.Testing, TimelineSeverity.Warning,
                $"{r.TestCode} invalidated", Join($"Specimen {accession}", $"Reason: {r.InvalidationReason}"),
                r.InvalidatedBy, href, $"res-{r.Id}-invalidate");
        }
    }

    /// <summary>Newest first; ties keep a stable order by category and source.</summary>
    internal static IReadOnlyList<TimelineEventDto> Order(IEnumerable<TimelineEventDto> events) => events
        .OrderByDescending(e => e.AtUtc)
        .ThenBy(e => e.Category)
        .ThenBy(e => e.SourceKey, StringComparer.Ordinal)
        .ToList();

    private static string? OverrideText(long? overrideId, IReadOnlyDictionary<long, Override> overrides)
    {
        if (overrideId is not long id)
        {
            return null;
        }

        if (!overrides.TryGetValue(id, out var o))
        {
            return "Issued with an override";
        }

        return Join($"Override {o.RuleCode}: {o.Reason}", string.IsNullOrWhiteSpace(o.AuthorizedBy) ? null : $"authorized by {o.AuthorizedBy}");
    }

    private static string Name(string? name, string? code) =>
        !string.IsNullOrWhiteSpace(name) ? name : !string.IsNullOrWhiteSpace(code) ? code : "unnamed";

    internal static string? Join(params string?[] parts)
    {
        var kept = parts.Where(p => !string.IsNullOrWhiteSpace(p)).ToList();
        return kept.Count == 0 ? null : string.Join(" · ", kept);
    }
}
