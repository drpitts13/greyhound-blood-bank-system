using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Common;
using BloodBankLIS.Application.Compatibility;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Domain.ValueObjects;

namespace BloodBankLIS.Application.Compliance;

public sealed record UpdateReactionInvestigationRequest(
    string? ReactionType,
    ReactionSeverity? Severity,
    string? Findings,
    string? Conclusions,
    string? FollowUp,
    string? Disposition,
    bool? ProductAtFault,
    bool? IsFatality,
    ReactionInvestigationStatus? Status,
    long? ClosedSignatureId = null,
    bool? ClericalCheckCompleted = null,
    string? ClericalCheckNotes = null,
    bool? VisualInspectionCompleted = null,
    bool? VisualInspectionAcceptable = null,
    string? RepeatPatientAboRh = null,
    string? RepeatUnitAboRh = null,
    DatWorkupResult? DatResult = null,
    string? ElutionResult = null,
    bool? RemainderQuarantined = null,
    /// <summary>OCD-057: the reaction concluded, a code from the reaction list (NONE for No reaction).</summary>
    string? ConcludedReactionCode = null,
    /// <summary>OCD-057: treatment codes from the treatment list (NONE for No treatment).</summary>
    IReadOnlyList<string>? TreatmentCodes = null);

public sealed record ReactionInvestigationDto(
    long Id,
    long TransfusionEventId,
    long PatientId,
    long BloodProductId,
    DateTime ReportedUtc,
    string ReportedBy,
    string? ReactionType,
    ReactionSeverity Severity,
    string? Findings,
    string? Conclusions,
    string? FollowUp,
    ReactionInvestigationStatus Status,
    string? Disposition,
    bool ProductAtFault,
    bool IsFatality,
    FatalityNotificationStatus FatalityNotificationStatus,
    DateTime? WrittenReportDueUtc,
    DateTime? CberNotifiedUtc,
    DateTime? WrittenReportSubmittedUtc,
    bool ClericalCheckCompleted = false,
    string? ClericalCheckNotes = null,
    bool VisualInspectionCompleted = false,
    bool VisualInspectionAcceptable = false,
    string? RepeatPatientAboRh = null,
    string? RepeatUnitAboRh = null,
    DatWorkupResult DatResult = DatWorkupResult.NotRecorded,
    string? ElutionResult = null,
    bool RemainderQuarantined = false,
    string? MedicalRecordNumber = null,
    string? PatientDisplayName = null,
    string? UnitNumber = null,
    string? CurrentBloodType = null,
    bool HasAntibodyHistory = false,
    string? AntibodySummary = null,
    bool WorkupIncomplete = false,
    string? UnitBloodType = null,
    string? WorkupHoldReason = null,
    bool AboRhIncompatible = false,
    string? AboRhCompatibilityAlert = null,
    IssueType? IssueType = null,
    CrossmatchClinicalStatus? CrossmatchStatus = null,
    bool TestsIncompleteAtIssue = false,
    bool RepeatPatientAboRhDiscrepancy = false,
    bool RepeatUnitAboRhDiscrepancy = false,
    string? RepeatAboRhDetail = null,
    UnitStatus? RemainderUnitStatus = null,
    string? RemainderHoldNote = null,
    bool FatalityFollowUpDue = false,
    string? FatalityFollowUpDetail = null,
    string? ClosedBy = null,
    DateTime? ClosedUtc = null,
    string? ReopenedBy = null,
    DateTime? ReopenedUtc = null,
    string? ReopenReason = null,
    bool ReportedAfterTransfusion = false,
    DateTime? OnsetUtc = null,
    string? ConcludedReactionCode = null,
    string? ConcludedReactionText = null,
    string? TreatmentCodes = null,
    string? TreatmentTexts = null,
    // The specimen workup: each specimen with its Test Group results (null until that specimen is chosen).
    ReactionWorkupSpecimenDto? PreWorkup = null,
    ReactionWorkupSpecimenDto? PostWorkup = null,
    // While not closed: the patient's units issued (not yet returned or transfused) and reserved (Cycle 630), so the
    // reactions list and home card can say another unit may be on its way to this patient (OCD-078).
    int PatientUnitsOutOrReserved = 0,
    // Units issued to the patient while this investigation was open or under review (Cycle 635), from the issue record.
    IReadOnlyList<ReactionIssuedWhileOpenDto>? IssuedWhileOpen = null)
{
    /// <summary>The specimen workup as the close gate sees it, from the results on this DTO.</summary>
    public ReactionWorkupCompletenessRule.WorkupState SpecimenWorkupState => new(
        PreWorkup is not null,
        PostWorkup is not null,
        PreWorkup?.Pending ?? 0,
        PostWorkup?.Pending ?? 0,
        string.Equals(PostWorkup?.VerifiedValue("DAT")?.Trim(), "Positive", StringComparison.OrdinalIgnoreCase));

    /// <summary>The one thing to do next, from <see cref="ReactionNextStepRule"/>.</summary>
    public ReactionNextStepRule.Step NextStep => ReactionNextStepRule.For(new ReactionNextStepRule.Facts(
        Status, FatalityFollowUpDue, FatalityFollowUpDetail, RemainderQuarantined, RemainderHoldNote,
        AboRhIncompatible || RepeatPatientAboRhDiscrepancy || RepeatUnitAboRhDiscrepancy,
        WorkupIncomplete, WorkupHoldReason,
        string.IsNullOrWhiteSpace(ConcludedReactionCode) || string.IsNullOrWhiteSpace(TreatmentCodes)));

    public static ReactionInvestigationDto From(
        ReactionInvestigation r,
        string? mrn = null,
        string? displayName = null,
        string? unitNumber = null,
        string? currentBloodType = null,
        bool hasAntibodyHistory = false,
        string? antibodySummary = null,
        string? unitBloodType = null,
        bool aboRhIncompatible = false,
        string? aboRhCompatibilityAlert = null,
        IssueType? issueType = null,
        CrossmatchClinicalStatus? crossmatchStatus = null,
        bool testsIncompleteAtIssue = false,
        bool repeatPatientAboRhDiscrepancy = false,
        bool repeatUnitAboRhDiscrepancy = false,
        string? repeatAboRhDetail = null,
        UnitStatus? remainderUnitStatus = null,
        string? remainderHoldNote = null,
        ReactionWorkupReader.Workup? specimenWorkup = null)
    {
        // A closed case passed the gate when it was signed; the gate is shown for open cases.
        var gate = ReactionWorkupCompletenessRule.Evaluate(
            r.ClericalCheckCompleted, r.VisualInspectionCompleted,
            ReactionWorkupReader.StateFor(r, specimenWorkup), r.ElutionResult);
        var workup = r.Status == ReactionInvestigationStatus.Closed ? RuleResult.Pass(gate.Code) : gate;
        var followUp = ReactionFatalityFollowUpRule.Evaluate(
            r.IsFatality, r.CberNotifiedUtc, r.WrittenReportSubmittedUtc);
        return new ReactionInvestigationDto(
            r.Id, r.TransfusionEventId, r.PatientId, r.BloodProductId, r.ReportedUtc, r.ReportedBy,
            r.ReactionType, r.Severity, r.Findings, r.Conclusions, r.FollowUp, r.Status, r.Disposition,
            r.ProductAtFault, r.IsFatality, r.FatalityNotificationStatus, r.WrittenReportDueUtc,
            r.CberNotifiedUtc, r.WrittenReportSubmittedUtc,
            r.ClericalCheckCompleted, r.ClericalCheckNotes, r.VisualInspectionCompleted,
            r.VisualInspectionAcceptable, r.RepeatPatientAboRh, r.RepeatUnitAboRh,
            r.DatResult, r.ElutionResult, r.RemainderQuarantined,
            mrn, displayName, unitNumber, currentBloodType, hasAntibodyHistory, antibodySummary,
            workup.Severity == RuleSeverity.HardStop,
            unitBloodType,
            workup.Severity == RuleSeverity.HardStop ? workup.Message : null,
            aboRhIncompatible,
            aboRhCompatibilityAlert,
            issueType,
            crossmatchStatus,
            testsIncompleteAtIssue,
            repeatPatientAboRhDiscrepancy,
            repeatUnitAboRhDiscrepancy,
            repeatAboRhDetail,
            remainderUnitStatus,
            remainderHoldNote,
            followUp.Severity == RuleSeverity.Warning,
            followUp.Severity == RuleSeverity.Warning ? followUp.Message : null,
            r.ClosedBy, r.ClosedUtc, r.ReopenedBy, r.ReopenedUtc, r.ReopenReason,
            r.ReportedAfterTransfusion, r.OnsetUtc,
            r.ConcludedReactionCode, r.ConcludedReactionText, r.TreatmentCodes, r.TreatmentTexts,
            specimenWorkup?.Pre, specimenWorkup?.Post);
    }
}

public sealed record ReopenReactionInvestigationRequest(string? Reason);

/// <summary>
/// The investigation on one sheet for the medical director's review and the record: the case with its workup
/// results, plus the issue and the bedside transfusion record (times, volume, what was seen and done).
/// </summary>
public sealed record ReactionReportDto(
    ReactionInvestigationDto Investigation,
    DateTime GeneratedUtc,
    string? ProductName,
    DateTime? IssuedUtc,
    string? IssuedBy,
    string? IssuedToLocation,
    DateTime? TransfusionStartUtc,
    DateTime? TransfusionStopUtc,
    decimal? VolumeTransfused,
    string? Transfusionist,
    string? TransfusionLocation,
    TransfusionDisposition? FinalDisposition,
    string? ReactionActions,
    string? PostTransfusionObservations,
    string? DocumentedBy,
    // Bedside vitals before and after, and the temperature change between them, when taken (end-user testing, 2026-11-12).
    string? VitalsBefore = null,
    string? VitalsAfter = null,
    string? TemperatureChange = null);

/// <summary>A reaction noticed after the transfusion was documented without one: what was seen, and when it began.</summary>
public sealed record ReportLateReactionRequest(long IssueId, string? Description, DateTime? OnsetUtc = null, string? ReactionType = null);

/// <summary>A unit issued to the patient while the investigation was open (Cycle 635).</summary>
public sealed record ReactionIssuedWhileOpenDto(long IssueId, long UnitId, string? UnitNumber, DateTime IssuedUtc, IssueStatus Status);

/// <summary>A patient's transfusion reaction investigation still open or under review (Cycle 632): number and report time.</summary>
public sealed record OpenReactionInvestigationDto(long Id, DateTime ReportedUtc);

public sealed class ReactionInvestigationService
{
    private readonly IRepository<ReactionInvestigation> _investigations;
    private readonly IRepository<TransfusionEvent> _transfusions;
    private readonly IInventoryRepository _inventory;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditWriter _audit;
    private readonly IPermissionEvaluator? _permissions;
    private readonly IRepository<Patient>? _patients;
    private readonly IRepository<PatientBloodTypeHistory>? _bloodTypes;
    private readonly IRepository<AntibodyHistory>? _antibodies;
    private readonly IRepository<Issue>? _issues;
    private readonly IRepository<ProductType>? _productTypes;
    private readonly IRepository<ProductAboCompatibility>? _aboCompatibilities;
    private readonly IRepository<ProductRhCompatibility>? _rhCompatibilities;
    private readonly IRepository<ReactionCatalogEntry>? _catalog;
    private readonly IRepository<Specimen>? _specimens;
    private readonly IRepository<Order>? _orders;
    private readonly IRepository<OrderLine>? _orderLines;
    private readonly IRepository<TestResult>? _testResults;
    private readonly IRepository<Allocation>? _allocations;

    public ReactionInvestigationService(
        IRepository<ReactionInvestigation> investigations,
        IRepository<TransfusionEvent> transfusions,
        IInventoryRepository inventory,
        IUnitOfWork unitOfWork,
        IClock clock,
        ICurrentUser currentUser,
        IAuditWriter audit,
        IPermissionEvaluator? permissions = null,
        IRepository<Patient>? patients = null,
        IRepository<PatientBloodTypeHistory>? bloodTypes = null,
        IRepository<AntibodyHistory>? antibodies = null,
        IRepository<Issue>? issues = null,
        IRepository<ProductType>? productTypes = null,
        IRepository<ProductAboCompatibility>? aboCompatibilities = null,
        IRepository<ProductRhCompatibility>? rhCompatibilities = null,
        IRepository<ReactionCatalogEntry>? catalog = null,
        IRepository<Specimen>? specimens = null,
        IRepository<Order>? orders = null,
        IRepository<OrderLine>? orderLines = null,
        IRepository<TestResult>? testResults = null,
        IRepository<Allocation>? allocations = null)
    {
        _allocations = allocations;
        _catalog = catalog;
        _specimens = specimens;
        _orders = orders;
        _orderLines = orderLines;
        _testResults = testResults;
        _investigations = investigations;
        _transfusions = transfusions;
        _inventory = inventory;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _currentUser = currentUser;
        _audit = audit;
        _permissions = permissions;
        _patients = patients;
        _bloodTypes = bloodTypes;
        _antibodies = antibodies;
        _issues = issues;
        _productTypes = productTypes;
        _aboCompatibilities = aboCompatibilities;
        _rhCompatibilities = rhCompatibilities;
    }

    public Task<IReadOnlyList<ReactionInvestigation>> ListAsync(CancellationToken ct = default) =>
        _investigations.ListAsync(ct);

    public Task<ReactionInvestigation?> GetAsync(long id, CancellationToken ct = default) =>
        _investigations.GetByIdAsync(id, ct);

    public async Task<IReadOnlyList<ReactionInvestigationDto>> ListDtosAsync(CancellationToken ct = default)
    {
        var rows = await _investigations.ListAsync(ct);
        var context = await LoadDisplayContextAsync(rows, ct);
        var unitsOut = await UnitsOutOrReservedByPatientAsync(
            rows.Where(r => r.Status != ReactionInvestigationStatus.Closed).Select(r => r.PatientId).Distinct().ToList(), ct);
        var issuedWhileOpen = await IssuedWhileOpenAsync(rows.Select(r => r.Id).ToList(), ct);
        return rows
            .OrderByDescending(r => r.ReportedUtc)
            .Select(r =>
            {
                var dto = context.ToDto(r) with { IssuedWhileOpen = issuedWhileOpen.GetValueOrDefault(r.Id) ?? [] };
                return r.Status == ReactionInvestigationStatus.Closed
                    ? dto
                    : dto with { PatientUnitsOutOrReserved = unitsOut.GetValueOrDefault(r.PatientId) };
            })
            .ToList();
    }

    /// <summary>
    /// Per patient, the latest investigation not yet closed (open or under review), for the bedside warning before
    /// another unit is started (Cycle 626). Read by issuing, so it needs no reaction-investigation permission.
    /// </summary>
    public async Task<IReadOnlyDictionary<long, (long Id, DateTime ReportedUtc)>> OpenByPatientAsync(
        IReadOnlyCollection<long> patientIds, CancellationToken ct = default)
    {
        if (patientIds.Count == 0)
        {
            return new Dictionary<long, (long, DateTime)>();
        }

        var rows = await _investigations.ListAsync(i => patientIds.Contains(i.PatientId)
            && i.Status != ReactionInvestigationStatus.Closed, ct);
        return rows
            .GroupBy(i => i.PatientId)
            .ToDictionary(g => g.Key, g =>
            {
                var latest = g.OrderByDescending(i => i.ReportedUtc).First();
                return (latest.Id, latest.ReportedUtc);
            });
    }

    /// <summary>Per patient, units issued and not yet returned or transfused, plus units reserved; 0 without the sources.</summary>
    private async Task<Dictionary<long, int>> UnitsOutOrReservedByPatientAsync(List<long> patientIds, CancellationToken ct)
    {
        var counts = new Dictionary<long, int>();
        if (patientIds.Count == 0)
        {
            return counts;
        }

        if (_issues is not null)
        {
            foreach (var issue in await _issues.ListAsync(i => patientIds.Contains(i.PatientId) && i.Status == IssueStatus.Issued, ct))
            {
                counts[issue.PatientId] = counts.GetValueOrDefault(issue.PatientId) + 1;
            }
        }

        if (_allocations is not null)
        {
            foreach (var allocation in await _allocations.ListAsync(a => patientIds.Contains(a.PatientId) && a.Status == AllocationStatus.Reserved, ct))
            {
                counts[allocation.PatientId] = counts.GetValueOrDefault(allocation.PatientId) + 1;
            }
        }

        return counts;
    }

    /// <summary>The patient's latest investigation not yet closed, as a list of none or one, for the chart banner.</summary>
    public async Task<IReadOnlyList<OpenReactionInvestigationDto>> OpenForPatientAsync(long patientId, CancellationToken ct = default) =>
        (await OpenByPatientAsync([patientId], ct)).TryGetValue(patientId, out var open)
            ? [new OpenReactionInvestigationDto(open.Id, open.ReportedUtc)]
            : [];

    public async Task<ReactionInvestigationDto?> GetDtoAsync(long id, CancellationToken ct = default)
    {
        var row = await _investigations.GetByIdAsync(id, ct);
        if (row is null)
        {
            return null;
        }

        var context = await LoadDisplayContextAsync([row], ct);
        var issuedWhileOpen = await IssuedWhileOpenAsync([row.Id], ct);
        return context.ToDto(row) with { IssuedWhileOpen = issuedWhileOpen.GetValueOrDefault(row.Id) ?? [] };
    }

    /// <summary>Per investigation, the issues recorded as made while it was open (Cycle 634's record), oldest first.</summary>
    private async Task<Dictionary<long, IReadOnlyList<ReactionIssuedWhileOpenDto>>> IssuedWhileOpenAsync(List<long> investigationIds, CancellationToken ct)
    {
        var result = new Dictionary<long, IReadOnlyList<ReactionIssuedWhileOpenDto>>();
        if (_issues is null || investigationIds.Count == 0)
        {
            return result;
        }

        var issues = await _issues.ListAsync(i => i.ReactionInvestigationOpenAtIssueId != null
            && investigationIds.Contains(i.ReactionInvestigationOpenAtIssueId.Value), ct);
        foreach (var group in issues.GroupBy(i => i.ReactionInvestigationOpenAtIssueId!.Value))
        {
            var list = new List<ReactionIssuedWhileOpenDto>();
            foreach (var issue in group.OrderBy(i => i.IssuedUtc))
            {
                var unit = await _inventory.GetUnitAsync(issue.BloodProductId, ct);
                list.Add(new ReactionIssuedWhileOpenDto(issue.Id, issue.BloodProductId, unit?.UnitNumber, issue.IssuedUtc, issue.Status));
            }

            result[group.Key] = list;
        }

        return result;
    }

    public async Task<ReactionInvestigation> OpenForTransfusionAsync(TransfusionEvent transfusion, CancellationToken ct = default)
    {
        var existing = await _investigations.FirstOrDefaultAsync(i => i.TransfusionEventId == transfusion.Id, ct);
        if (existing is not null)
        {
            return existing;
        }

        var row = new ReactionInvestigation
        {
            TransfusionEventId = transfusion.Id,
            PatientId = transfusion.PatientId,
            BloodProductId = transfusion.BloodProductId,
            ReportedUtc = _clock.UtcNow,
            ReportedBy = _currentUser.UserName,
            Status = ReactionInvestigationStatus.Open
        };

        await TryQuarantineRemainderAsync(row, ct);

        await _investigations.AddAsync(row, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        _audit.Record(
            AuditEventType.ReactionInvestigation,
            nameof(ReactionInvestigation),
            row.Id,
            newValue: new
            {
                TransfusionEventId = transfusion.Id,
                row.PatientId,
                row.BloodProductId,
                row.Status,
                row.RemainderQuarantined
            },
            reason: "Reaction suspected");
        await _unitOfWork.SaveChangesAsync(ct);
        return row;
    }

    /// <summary>
    /// Opens an investigation for a reaction noticed after the transfusion was documented without one. The transfusion
    /// is marked reaction-suspected (the change is audited with its earlier value), so the chart, lookback, and the
    /// patient's next issue show it; the remainder of the unit is quarantined as for a bedside report.
    /// </summary>
    public async Task<OperationResult<ReactionInvestigation>> ReportLateAsync(ReportLateReactionRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var allowed = _permissions is null
            || await _permissions.HasPermissionAsync(_currentUser.UserName, PermissionCodes.TransfusionDocument, ct)
            || await _permissions.HasPermissionAsync(_currentUser.UserName, PermissionCodes.ReactionInvestigate, ct);
        var transfusion = (await _transfusions.ListAsync(t => t.IssueId == request.IssueId, ct))
            .OrderByDescending(t => t.Id)
            .FirstOrDefault();
        var existing = transfusion is null
            ? null
            : await _investigations.FirstOrDefaultAsync(i => i.TransfusionEventId == transfusion.Id, ct);
        var onset = request.OnsetUtc is { } o
            ? o.Kind switch
            {
                DateTimeKind.Unspecified => DateTime.SpecifyKind(o, DateTimeKind.Utc),
                DateTimeKind.Local => o.ToUniversalTime(),
                _ => o
            }
            : (DateTime?)null;
        var check = LateReactionReportRule.Evaluate(
            allowed, transfusion is not null, existing?.Id, transfusion?.StartUtc, onset, request.Description, _clock.UtcNow);
        if (check.Severity == RuleSeverity.HardStop)
        {
            return OperationResult<ReactionInvestigation>.Fail($"{check.Code}: {check.Message}");
        }

        var tracked = await _transfusions.GetByIdAsync(transfusion!.Id, ct) ?? transfusion;
        var description = request.Description!.Trim();
        var row = new ReactionInvestigation
        {
            TransfusionEventId = tracked.Id,
            PatientId = tracked.PatientId,
            BloodProductId = tracked.BloodProductId,
            ReportedUtc = _clock.UtcNow,
            ReportedBy = _currentUser.UserName,
            ReactionType = string.IsNullOrWhiteSpace(request.ReactionType) ? null : request.ReactionType.Trim(),
            Findings = $"Reported after the transfusion: {description}",
            Status = ReactionInvestigationStatus.Open,
            ReportedAfterTransfusion = true,
            OnsetUtc = onset
        };

        await TryQuarantineRemainderAsync(row, ct);
        await _investigations.AddAsync(row, ct);

        var wasSuspected = tracked.ReactionSuspected;
        tracked.ReactionSuspected = true;
        _transfusions.Update(tracked);
        await _unitOfWork.SaveChangesAsync(ct);

        _audit.Record(
            AuditEventType.ReactionInvestigation,
            nameof(ReactionInvestigation),
            row.Id,
            newValue: new
            {
                TransfusionEventId = tracked.Id,
                row.PatientId,
                row.BloodProductId,
                row.Status,
                row.RemainderQuarantined,
                row.ReportedAfterTransfusion,
                row.OnsetUtc,
                row.ReactionType
            },
            reason: $"Reaction reported after the transfusion: {description}");
        _audit.Record(
            AuditEventType.Transfusion,
            nameof(TransfusionEvent),
            tracked.Id,
            oldValue: new { ReactionSuspected = wasSuspected },
            newValue: new { tracked.ReactionSuspected, ReactionInvestigationId = row.Id },
            reason: "Reaction reported after the transfusion was documented.");
        await _unitOfWork.SaveChangesAsync(ct);
        return OperationResult<ReactionInvestigation>.Ok(row);
    }

    public async Task<OperationResult<ReactionInvestigation>> UpdateAsync(
        long id, UpdateReactionInvestigationRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var denied = await RejectUnauthorizedAsync(ct);
        if (denied is not null)
        {
            return denied;
        }

        var row = await _investigations.GetByIdAsync(id, ct);
        if (row is null)
        {
            return OperationResult<ReactionInvestigation>.Fail("Investigation not found.");
        }

        var locked = ReactionInvestigationLockRule.EvaluateUpdate(row.Status, ChangesFindings(row, request), request.Status);
        if (locked.Severity == RuleSeverity.HardStop)
        {
            return OperationResult<ReactionInvestigation>.Fail($"{locked.Code}: {locked.Message}");
        }

        var oldValue = Snapshot(row);

        if (request.ReactionType is not null) row.ReactionType = request.ReactionType;
        if (request.Severity is not null) row.Severity = request.Severity.Value;
        if (request.Findings is not null) row.Findings = request.Findings;
        if (request.Conclusions is not null) row.Conclusions = request.Conclusions;
        if (request.FollowUp is not null) row.FollowUp = request.FollowUp;
        if (request.Disposition is not null) row.Disposition = request.Disposition;
        if (request.ProductAtFault is not null) row.ProductAtFault = request.ProductAtFault.Value;
        if (request.ClericalCheckCompleted is not null) row.ClericalCheckCompleted = request.ClericalCheckCompleted.Value;
        if (request.ClericalCheckNotes is not null) row.ClericalCheckNotes = request.ClericalCheckNotes;
        if (request.VisualInspectionCompleted is not null) row.VisualInspectionCompleted = request.VisualInspectionCompleted.Value;
        if (request.VisualInspectionAcceptable is not null) row.VisualInspectionAcceptable = request.VisualInspectionAcceptable.Value;
        if (request.RepeatPatientAboRh is not null) row.RepeatPatientAboRh = request.RepeatPatientAboRh;
        if (request.RepeatUnitAboRh is not null) row.RepeatUnitAboRh = request.RepeatUnitAboRh;
        if (request.DatResult is not null) row.DatResult = request.DatResult.Value;
        if (request.ElutionResult is not null) row.ElutionResult = request.ElutionResult;

        RuleResult? remainderWarning = null;
        if (request.RemainderQuarantined == true && !row.RemainderQuarantined)
        {
            var holdProblem = await TryQuarantineRemainderAsync(row, ct);
            if (holdProblem is not null)
            {
                remainderWarning = RuleResult.Warning(RemainderHoldCode, holdProblem);
            }
        }

        // OCD-057: the conclusion comes from the admin lists; it is required to close.
        var conclusionSent = request.ConcludedReactionCode is not null || request.TreatmentCodes is not null;
        var closing = request.Status == ReactionInvestigationStatus.Closed;
        if (conclusionSent || closing)
        {
            var entries = _catalog is null ? [] : await _catalog.ListAsync(e => e.IsActive, ct);
            var reaction = request.ConcludedReactionCode ?? row.ConcludedReactionCode;
            var treatments = request.TreatmentCodes ?? SplitCodes(row.TreatmentCodes);
            var conclusion = ReactionConclusionRule.Evaluate(reaction, treatments, entries, requireComplete: closing);
            if (conclusion.Severity == RuleSeverity.HardStop)
            {
                return OperationResult<ReactionInvestigation>.Fail($"{conclusion.Code}: {conclusion.Message}");
            }

            if (request.ConcludedReactionCode is not null)
            {
                var code = ReactionConclusionRule.Normalize(request.ConcludedReactionCode);
                row.ConcludedReactionCode = code.Length == 0 ? null : code;
                row.ConcludedReactionText = code.Length == 0 ? null : ReactionConclusionRule.TextFor(code, ReactionCatalogKind.ReactionType, entries);
            }

            if (request.TreatmentCodes is not null)
            {
                var codes = request.TreatmentCodes.Select(ReactionConclusionRule.Normalize).Where(c => c.Length > 0).Distinct().ToList();
                row.TreatmentCodes = codes.Count == 0 ? null : string.Join("|", codes);
                row.TreatmentTexts = codes.Count == 0
                    ? null
                    : string.Join("; ", codes.Select(c => ReactionConclusionRule.TextFor(c, ReactionCatalogKind.Treatment, entries)));
            }
        }

        if ((request.IsFatality == true || request.Severity == ReactionSeverity.Fatal) && !row.IsFatality)
        {
            row.IsFatality = true;
            row.Severity = ReactionSeverity.Fatal;
            row.FatalityNotificationStatus = FatalityNotificationStatus.Pending;
            row.WrittenReportDueUtc = _clock.UtcNow.AddDays(7);
        }

        if (request.Status == ReactionInvestigationStatus.Closed)
        {
            if (request.ClosedSignatureId is null or <= 0)
            {
                return OperationResult<ReactionInvestigation>.Fail("Closing an investigation requires an electronic signature.");
            }

            // Both workup specimens with every Test Group test verified (a missing read counts as unverified).
            var specimenWorkup = (await LoadWorkupsAsync([row], ct)).GetValueOrDefault(row.Id);
            var workup = ReactionWorkupCompletenessRule.Evaluate(
                row.ClericalCheckCompleted,
                row.VisualInspectionCompleted,
                ReactionWorkupReader.StateFor(row, specimenWorkup),
                row.ElutionResult);
            if (workup.Severity == RuleSeverity.HardStop)
            {
                return OperationResult<ReactionInvestigation>.Fail($"{workup.Code}: {workup.Message}");
            }

            row.Status = ReactionInvestigationStatus.Closed;
            row.ClosedSignatureId = request.ClosedSignatureId;
            row.ClosedBy = _currentUser.UserName;
            row.ClosedUtc = _clock.UtcNow;
        }
        else if (request.Status is not null)
        {
            row.Status = request.Status.Value;
        }

        _audit.Record(
            AuditEventType.ReactionInvestigation,
            nameof(ReactionInvestigation),
            row.Id,
            oldValue: oldValue,
            newValue: Snapshot(row),
            reason: "Investigation updated.");
        await _unitOfWork.SaveChangesAsync(ct);
        return OperationResult<ReactionInvestigation>.Ok(
            row,
            remainderWarning is null ? null : [remainderWarning]);
    }

    /// <summary>
    /// Reopens a signed investigation so it can be changed. Needs a reason. The audit keeps the closing signature,
    /// who closed it, and when; the case must be closed again with a new signature.
    /// </summary>
    public async Task<OperationResult<ReactionInvestigation>> ReopenAsync(
        long id, ReopenReactionInvestigationRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var denied = await RejectUnauthorizedAsync(ct);
        if (denied is not null)
        {
            return denied;
        }

        var row = await _investigations.GetByIdAsync(id, ct);
        if (row is null)
        {
            return OperationResult<ReactionInvestigation>.Fail("Investigation not found.");
        }

        var check = ReactionInvestigationLockRule.EvaluateReopen(row.Status, request.Reason);
        if (check.Severity == RuleSeverity.HardStop)
        {
            return OperationResult<ReactionInvestigation>.Fail($"{check.Code}: {check.Message}");
        }

        var oldValue = new { row.Status, row.ClosedBy, row.ClosedUtc, row.ClosedSignatureId };
        row.Status = ReactionInvestigationStatus.UnderReview;
        row.ClosedBy = null;
        row.ClosedUtc = null;
        row.ClosedSignatureId = null;
        row.ReopenedUtc = _clock.UtcNow;
        row.ReopenedBy = _currentUser.UserName;
        row.ReopenReason = request.Reason!.Trim();
        _investigations.Update(row);
        _audit.Record(
            AuditEventType.ReactionInvestigation,
            nameof(ReactionInvestigation),
            row.Id,
            oldValue: oldValue,
            newValue: new { row.Status, Action = "Reopened", row.ReopenedBy, row.ReopenedUtc },
            reason: row.ReopenReason);
        await _unitOfWork.SaveChangesAsync(ct);
        return OperationResult<ReactionInvestigation>.Ok(row);
    }

    /// <summary>True when the request would change any workup or conclusion field (not the status).</summary>
    private static bool ChangesFindings(ReactionInvestigation row, UpdateReactionInvestigationRequest r) =>
        Differs(r.ReactionType, row.ReactionType)
        || (r.Severity is not null && r.Severity != row.Severity)
        || Differs(r.Findings, row.Findings)
        || Differs(r.Conclusions, row.Conclusions)
        || Differs(r.FollowUp, row.FollowUp)
        || Differs(r.Disposition, row.Disposition)
        || (r.ProductAtFault is not null && r.ProductAtFault != row.ProductAtFault)
        || (r.IsFatality == true && !row.IsFatality)
        || (r.ClericalCheckCompleted is not null && r.ClericalCheckCompleted != row.ClericalCheckCompleted)
        || Differs(r.ClericalCheckNotes, row.ClericalCheckNotes)
        || (r.VisualInspectionCompleted is not null && r.VisualInspectionCompleted != row.VisualInspectionCompleted)
        || (r.VisualInspectionAcceptable is not null && r.VisualInspectionAcceptable != row.VisualInspectionAcceptable)
        || Differs(r.RepeatPatientAboRh, row.RepeatPatientAboRh)
        || Differs(r.RepeatUnitAboRh, row.RepeatUnitAboRh)
        || (r.DatResult is not null && r.DatResult != row.DatResult)
        || Differs(r.ElutionResult, row.ElutionResult)
        || (r.RemainderQuarantined == true && !row.RemainderQuarantined)
        || Differs(r.ConcludedReactionCode, row.ConcludedReactionCode)
        || (r.TreatmentCodes is not null && string.Join("|", r.TreatmentCodes.Select(ReactionConclusionRule.Normalize).Where(c => c.Length > 0).Distinct()) != (row.TreatmentCodes ?? string.Empty));

    private static IReadOnlyList<string> SplitCodes(string? joined) =>
        string.IsNullOrWhiteSpace(joined) ? [] : joined.Split('|', StringSplitOptions.RemoveEmptyEntries);

    // A null request field means "not sent"; blank and null stored values are the same.
    private static bool Differs(string? requested, string? stored) =>
        requested is not null && !string.Equals(Normalize(requested), Normalize(stored), StringComparison.Ordinal);

    private static string Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value;

    public const string RemainderHoldCode = "RXN-REMAINDER-HOLD";

    /// <summary>
    /// Keeps the single status label aligned with the timestamps. A written report
    /// recorded before CBER stays Pending so the label does not say the follow-up is finished.
    /// Recording CBER afterward does not clear the written-report timestamp.
    /// </summary>
    private static void ApplyFatalityProgress(ReactionInvestigation row)
    {
        if (row.CberNotifiedUtc is not null && row.WrittenReportSubmittedUtc is not null)
        {
            row.FatalityNotificationStatus = FatalityNotificationStatus.WrittenReportSubmitted;
            return;
        }

        if (row.CberNotifiedUtc is not null)
        {
            row.FatalityNotificationStatus = FatalityNotificationStatus.CberNotified;
            return;
        }

        if (row.IsFatality)
        {
            row.FatalityNotificationStatus = FatalityNotificationStatus.Pending;
        }
    }

    public async Task<OperationResult<ReactionInvestigation>> RecordCberNotificationAsync(long id, CancellationToken ct = default)
    {
        var denied = await RejectUnauthorizedAsync(ct);
        if (denied is not null)
        {
            return denied;
        }

        var row = await _investigations.GetByIdAsync(id, ct);
        if (row is null)
        {
            return OperationResult<ReactionInvestigation>.Fail("Investigation not found.");
        }

        if (!row.IsFatality)
        {
            return OperationResult<ReactionInvestigation>.Fail("CBER notification is recorded only for a fatality.");
        }

        if (row.CberNotifiedUtc is not null)
        {
            return OperationResult<ReactionInvestigation>.Fail("CBER notification is already recorded.");
        }

        var oldStatus = row.FatalityNotificationStatus;
        var oldNotified = row.CberNotifiedUtc;
        row.CberNotifiedUtc = _clock.UtcNow;
        ApplyFatalityProgress(row);
        _audit.Record(
            AuditEventType.ReactionInvestigation,
            nameof(ReactionInvestigation),
            row.Id,
            oldValue: new { FatalityNotificationStatus = oldStatus, CberNotifiedUtc = oldNotified },
            newValue: new { row.FatalityNotificationStatus, row.CberNotifiedUtc },
            reason: "CBER notification recorded.");
        await _unitOfWork.SaveChangesAsync(ct);
        return OperationResult<ReactionInvestigation>.Ok(row);
    }

    /// <summary>The investigation report, with the issue and transfusion record. Needs reaction.investigate.</summary>
    public async Task<OperationResult<ReactionReportDto>> GetReportAsync(long id, CancellationToken ct = default)
    {
        var denied = await RejectUnauthorizedAsync(ct);
        if (denied is not null)
        {
            return OperationResult<ReactionReportDto>.Fail(denied.Error!);
        }

        var dto = await GetDtoAsync(id, ct);
        if (dto is null)
        {
            return OperationResult<ReactionReportDto>.Fail("Investigation not found.");
        }

        var transfusion = await _transfusions.GetByIdAsync(dto.TransfusionEventId, ct);
        var issue = transfusion is null || _issues is null ? null : await _issues.GetByIdAsync(transfusion.IssueId, ct);
        var unit = await _inventory.GetUnitAsync(dto.BloodProductId, ct);
        var product = unit is null || _productTypes is null ? null : await _productTypes.GetByIdAsync(unit.ProductTypeId, ct);
        return OperationResult<ReactionReportDto>.Ok(new ReactionReportDto(
            dto,
            _clock.UtcNow,
            product?.Name,
            issue?.IssuedUtc,
            issue?.IssuedBy,
            issue?.IssuedToLocation,
            transfusion?.StartUtc,
            transfusion?.StopUtc,
            transfusion?.VolumeTransfused,
            transfusion?.Transfusionist,
            transfusion?.Location,
            transfusion?.FinalDisposition,
            transfusion?.ReactionActions,
            transfusion?.PostTransfusionObservations,
            transfusion?.DocumentedBy,
            BloodBankLIS.Domain.ValueObjects.TransfusionVitals.FromJson(transfusion?.PreTransfusionVitalsJson)?.Describe(),
            BloodBankLIS.Domain.ValueObjects.TransfusionVitals.FromJson(transfusion?.VitalsJson)?.Describe(),
            BloodBankLIS.Domain.ValueObjects.TransfusionVitals.TemperatureChange(
                BloodBankLIS.Domain.ValueObjects.TransfusionVitals.FromJson(transfusion?.PreTransfusionVitalsJson),
                BloodBankLIS.Domain.ValueObjects.TransfusionVitals.FromJson(transfusion?.VitalsJson))));
    }

    /// <summary>
    /// Records the report print before the browser prints it: the sheet carries patient data off the system. The
    /// audit names whether the case was open or closed when printed, since an open case's sheet is a draft.
    /// </summary>
    public async Task<OperationResult<ReactionInvestigation>> RecordReportPrintedAsync(long id, CancellationToken ct = default)
    {
        var denied = await RejectUnauthorizedAsync(ct);
        if (denied is not null)
        {
            return denied;
        }

        var row = await _investigations.GetByIdAsync(id, ct);
        if (row is null)
        {
            return OperationResult<ReactionInvestigation>.Fail("Investigation not found.");
        }

        _audit.Record(
            AuditEventType.Print,
            nameof(ReactionInvestigation),
            row.Id,
            newValue: new { Document = "Transfusion reaction investigation report", row.PatientId, row.BloodProductId, row.Status },
            reason: row.Status == ReactionInvestigationStatus.Closed
                ? "Reaction investigation report printed."
                : "Reaction investigation report printed while the case is open (draft).");
        await _unitOfWork.SaveChangesAsync(ct);
        return OperationResult<ReactionInvestigation>.Ok(row);
    }

    public async Task<OperationResult<ReactionInvestigation>> RecordWrittenReportAsync(long id, CancellationToken ct = default)
    {
        var denied = await RejectUnauthorizedAsync(ct);
        if (denied is not null)
        {
            return denied;
        }

        var row = await _investigations.GetByIdAsync(id, ct);
        if (row is null)
        {
            return OperationResult<ReactionInvestigation>.Fail("Investigation not found.");
        }

        if (!row.IsFatality)
        {
            return OperationResult<ReactionInvestigation>.Fail("The written report is recorded only for a fatality.");
        }

        if (row.WrittenReportSubmittedUtc is not null)
        {
            return OperationResult<ReactionInvestigation>.Fail("The written report is already recorded.");
        }

        var oldStatus = row.FatalityNotificationStatus;
        var oldSubmitted = row.WrittenReportSubmittedUtc;
        row.WrittenReportSubmittedUtc = _clock.UtcNow;
        ApplyFatalityProgress(row);
        _audit.Record(
            AuditEventType.ReactionInvestigation,
            nameof(ReactionInvestigation),
            row.Id,
            oldValue: new { FatalityNotificationStatus = oldStatus, WrittenReportSubmittedUtc = oldSubmitted },
            newValue: new { row.FatalityNotificationStatus, row.WrittenReportSubmittedUtc },
            reason: "Written fatality report recorded.");
        await _unitOfWork.SaveChangesAsync(ct);
        return OperationResult<ReactionInvestigation>.Ok(row);
    }

    private static object Snapshot(ReactionInvestigation row) => new
    {
        row.Status,
        row.ReactionType,
        row.Severity,
        row.Findings,
        row.Conclusions,
        row.FollowUp,
        row.Disposition,
        row.ClericalCheckNotes,
        row.VisualInspectionAcceptable,
        row.RepeatPatientAboRh,
        row.RepeatUnitAboRh,
        row.DatResult,
        row.ElutionResult,
        row.ClericalCheckCompleted,
        row.VisualInspectionCompleted,
        row.RemainderQuarantined,
        row.IsFatality,
        row.ProductAtFault,
        row.ConcludedReactionCode,
        row.ConcludedReactionText,
        row.TreatmentCodes,
        row.TreatmentTexts,
        row.PreTransfusionSpecimenId,
        row.PreTransfusionOrderId,
        row.PostTransfusionSpecimenId,
        row.PostTransfusionOrderId
    };

    private static bool CanHoldReactionRemainder(UnitStatus status) =>
        status is UnitStatus.Quarantine
            or UnitStatus.Issued
            or UnitStatus.TransfusionStarted
            or UnitStatus.TransfusionStopped
            or UnitStatus.Transfused
            or UnitStatus.Returned
            or UnitStatus.ReturnPending
        || InventoryStatusTransition.IsAllowed(status, UnitStatus.Quarantine);

    /// <summary>Null when the remainder is held. Otherwise a reason the investigation can still be saved.</summary>
    private async Task<string?> TryQuarantineRemainderAsync(ReactionInvestigation row, CancellationToken ct)
    {
        var unit = await _inventory.GetUnitAsync(row.BloodProductId, ct);
        if (unit is null)
        {
            return "Unit was not found, so the remainder was not held. The investigation was saved.";
        }

        if (unit.Status == UnitStatus.Quarantine)
        {
            row.RemainderQuarantined = true;
            if (unit.QuarantineReasonCode == UnitQuarantineReason.Unspecified
                || unit.QuarantineReasonCode == UnitQuarantineReason.Other)
            {
                unit.QuarantineReasonCode = UnitQuarantineReason.ReactionRemainder;
            }

            return null;
        }

        if (!CanHoldReactionRemainder(unit.Status))
        {
            return $"Unit is {unit.Status} and cannot be moved to quality quarantine. The investigation was saved; remainder is not held.";
        }

        const string reason = "Transfusion reaction investigation — remainder or segments held.";
        var from = unit.Status;
        unit.Status = UnitStatus.Quarantine;
        unit.QuarantineReason = reason;
        unit.QuarantineReasonCode = UnitQuarantineReason.ReactionRemainder;
        _inventory.AddStatusHistory(new InventoryStatusHistory
        {
            BloodProductId = unit.Id,
            FromStatus = from,
            ToStatus = UnitStatus.Quarantine,
            FromLocationId = unit.CurrentLocationId,
            ToLocationId = unit.CurrentLocationId,
            Reason = reason,
            ChangedBy = _currentUser.UserName,
            ChangedUtc = _clock.UtcNow
        });
        row.RemainderQuarantined = true;
        return null;
    }

    private async Task<OperationResult<ReactionInvestigation>?> RejectUnauthorizedAsync(CancellationToken ct)
    {
        if (_permissions is null)
        {
            return null;
        }

        var allowed = await _permissions.HasPermissionAsync(
            _currentUser.UserName, PermissionCodes.ReactionInvestigate, ct);
        var auth = ReactionAuthorizationRule.EvaluateInvestigate(allowed);
        return auth.Severity == RuleSeverity.HardStop
            ? OperationResult<ReactionInvestigation>.Fail(auth.Message)
            : null;
    }

    private async Task<ReactionDisplayContext> LoadDisplayContextAsync(
        IReadOnlyList<ReactionInvestigation> rows, CancellationToken ct)
    {
        var patientIds = rows.Select(r => r.PatientId).Distinct().ToList();
        var patients = _patients is null || patientIds.Count == 0
            ? []
            : await _patients.ListAsync(p => patientIds.Contains(p.Id), ct);
        var types = _bloodTypes is null || patientIds.Count == 0
            ? []
            : await _bloodTypes.ListAsync(h => patientIds.Contains(h.PatientId) && h.IsCurrent, ct);
        var antibodies = _antibodies is null || patientIds.Count == 0
            ? []
            : await _antibodies.ListAsync(a => patientIds.Contains(a.PatientId), ct);

        var units = new Dictionary<long, BloodUnit>();
        foreach (var unitId in rows.Select(r => r.BloodProductId).Distinct())
        {
            var unit = await _inventory.GetUnitAsync(unitId, ct);
            if (unit is not null)
            {
                units[unitId] = unit;
            }
        }

        var productIds = units.Values.Select(u => u.ProductTypeId).Distinct().ToList();
        var products = _productTypes is null || productIds.Count == 0
            ? []
            : await _productTypes.ListAsync(p => productIds.Contains(p.Id), ct);
        var aboRhPolicies = await ProductAboRhPolicyLoader.ForProductsAsync(
            _aboCompatibilities, _rhCompatibilities, productIds, ct);

        var transfusionIds = rows.Select(r => r.TransfusionEventId).Distinct().ToList();
        var transfusions = transfusionIds.Count == 0
            ? []
            : await _transfusions.ListAsync(t => transfusionIds.Contains(t.Id), ct);
        var issueIds = transfusions.Select(t => t.IssueId).Distinct().ToList();
        var issues = _issues is null || issueIds.Count == 0
            ? []
            : await _issues.ListAsync(i => issueIds.Contains(i.Id), ct);

        var workups = await LoadWorkupsAsync(rows, ct);
        return new ReactionDisplayContext(patients, types, antibodies, units, products, transfusions, issues, aboRhPolicies, workups);
    }

    private Task<IReadOnlyDictionary<long, ReactionWorkupReader.Workup>> LoadWorkupsAsync(
        IReadOnlyList<ReactionInvestigation> rows, CancellationToken ct) =>
        ReactionWorkupReader.LoadAsync(rows, _specimens, _orders, _orderLines, _testResults, _clock.UtcNow, ct);

    private sealed class ReactionDisplayContext(
        IReadOnlyList<Patient> patients,
        IReadOnlyList<PatientBloodTypeHistory> types,
        IReadOnlyList<AntibodyHistory> antibodies,
        IReadOnlyDictionary<long, BloodUnit> units,
        IReadOnlyList<ProductType> products,
        IReadOnlyList<TransfusionEvent> transfusions,
        IReadOnlyList<Issue> issues,
        IReadOnlyDictionary<long, ProductAboRhPolicy> aboRhPolicies,
        IReadOnlyDictionary<long, ReactionWorkupReader.Workup> workups)
    {
        private readonly IReadOnlyDictionary<long, ReactionWorkupReader.Workup> _workups = workups;
        private readonly Dictionary<long, Patient> _patients = patients.ToDictionary(p => p.Id);
        private readonly Dictionary<long, PatientBloodTypeHistory> _types = types
            .GroupBy(h => h.PatientId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(h => h.Id).First());
        private readonly Dictionary<long, List<AntibodyHistory>> _antibodies = antibodies
            .GroupBy(a => a.PatientId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(a => a.IsActive).ThenBy(a => a.AntibodySpecificity).ToList());
        private readonly IReadOnlyDictionary<long, BloodUnit> _units = units;
        private readonly Dictionary<long, ProductType> _products = products.ToDictionary(p => p.Id);
        private readonly Dictionary<long, TransfusionEvent> _transfusions = transfusions.ToDictionary(t => t.Id);
        private readonly Dictionary<long, Issue> _issues = issues.ToDictionary(i => i.Id);
        private readonly IReadOnlyDictionary<long, ProductAboRhPolicy> _aboRhPolicies = aboRhPolicies;

        public ReactionInvestigationDto ToDto(ReactionInvestigation row)
        {
            _patients.TryGetValue(row.PatientId, out var patient);
            _types.TryGetValue(row.PatientId, out var type);
            _antibodies.TryGetValue(row.PatientId, out var history);
            _units.TryGetValue(row.BloodProductId, out var unit);
            ProductType? product = null;
            if (unit is not null)
            {
                _products.TryGetValue(unit.ProductTypeId, out product);
            }

            _transfusions.TryGetValue(row.TransfusionEventId, out var transfusion);
            Issue? issue = null;
            if (transfusion is not null)
            {
                _issues.TryGetValue(transfusion.IssueId, out issue);
            }

            ProductAboRhPolicy? aboRhPolicy = null;
            if (product is not null)
            {
                _aboRhPolicies.TryGetValue(product.Id, out aboRhPolicy);
            }

            var (incompatible, alert) = EvaluateLabeledCompatibility(
                type, unit, product, patient?.Sex ?? Sex.Unknown, aboRhPolicy);
            var labeledUnit = unit is null ? (AboRh?)null : new AboRh(unit.Abo, unit.RhD);
            // The repeat patient type is the verified ABO/Rh on the post-transfusion specimen; a case recorded before the
            // specimen workup keeps its typed value. The pre- and post-transfusion types are also compared.
            _workups.TryGetValue(row.Id, out var specimenWorkup);
            var postAbo = specimenWorkup?.Post?.VerifiedValue("ABORH");
            var preAbo = specimenWorkup?.Pre?.VerifiedValue("ABORH");
            var patientRepeatText = postAbo ?? (row.PostTransfusionOrderId is null ? row.RepeatPatientAboRh : null);
            var patientRepeat = ReactionRepeatAboRhRule.Evaluate(type?.BloodType, patientRepeatText, "Patient");
            var prePost = preAbo is not null && postAbo is not null && ReactionRepeatAboRhRule.TryParseDisplay(preAbo, out var preType)
                ? ReactionRepeatAboRhRule.Evaluate(preType, postAbo, "Post-transfusion specimen")
                : RuleResult.Pass(ReactionRepeatAboRhRule.Code);
            var unitRepeat = ReactionRepeatAboRhRule.Evaluate(
                labeledUnit, row.RepeatUnitAboRh, "Unit");
            var patientDelta = patientRepeat.Severity == RuleSeverity.Warning || prePost.Severity == RuleSeverity.Warning;
            var unitDelta = unitRepeat.Severity == RuleSeverity.Warning;
            var repeatDetail = string.Join(" ", new[]
            {
                patientRepeat.Severity == RuleSeverity.Warning ? patientRepeat.Message : null,
                prePost.Severity == RuleSeverity.Warning ? prePost.Message.Replace("disagrees with", "disagrees with the pre-transfusion specimen's", StringComparison.Ordinal) : null,
                unitDelta ? unitRepeat.Message : null
            }.Where(m => !string.IsNullOrWhiteSpace(m)));
            return ReactionInvestigationDto.From(
                row,
                patient?.MedicalRecordNumber,
                patient is null ? null : $"{patient.LastName}, {patient.FirstName}",
                unit?.UnitNumber,
                type?.BloodType.ToString(),
                history is { Count: > 0 },
                FormatAntibodySummary(history),
                labeledUnit?.ToString(),
                incompatible,
                alert,
                issue?.IssueType,
                issue?.CrossmatchStatus,
                issue?.TestsIncompleteAtIssue ?? false,
                patientDelta,
                unitDelta,
                string.IsNullOrWhiteSpace(repeatDetail) ? null : repeatDetail,
                unit?.Status,
                RemainderNote(row.RemainderQuarantined, unit?.Status),
                specimenWorkup);
        }

        private static string? RemainderNote(bool quarantined, UnitStatus? status)
        {
            if (quarantined)
            {
                return null;
            }

            if (status is null)
            {
                return "Unit record was not found, so the remainder is not held.";
            }

            if (!CanHoldReactionRemainder(status.Value))
            {
                return $"Unit is {status.Value} and cannot be moved to quality quarantine.";
            }

            return null;
        }

        private static (bool Incompatible, string? Alert) EvaluateLabeledCompatibility(
            PatientBloodTypeHistory? type,
            BloodUnit? unit,
            ProductType? product,
            Sex patientSex,
            ProductAboRhPolicy? policy)
        {
            if (type is null || unit is null)
            {
                return (false, null);
            }

            var component = product?.ComponentClass ?? ComponentClass.RedBloodCells;
            var firstStop = AboCompatibilityRule.Evaluate(
                    type.BloodType, new AboRh(unit.Abo, unit.RhD), component, patientSex, policy)
                .FirstOrDefault(r => r.Severity == RuleSeverity.HardStop);
            return firstStop is null ? (false, null) : (true, firstStop.Message);
        }

        private static string? FormatAntibodySummary(IReadOnlyList<AntibodyHistory>? history)
        {
            if (history is not { Count: > 0 })
            {
                return null;
            }

            // One wording with the chart header: an antibody known only from another facility's report says so.
            return string.Join(", ", history.Select(a =>
                OutsideAntibodyReportRule.HistoryEntry(a.AntibodySpecificity, a.IsActive, a.ReportingFacility, a.IdentifiedOn, a.ConfirmedUtc is not null)));
        }
    }
}
