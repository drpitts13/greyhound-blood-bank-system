using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Common;
using BloodBankLIS.Domain.Entities;
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
    bool? RemainderQuarantined = null);

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
    bool TestsIncompleteAtIssue = false)
{
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
        bool testsIncompleteAtIssue = false)
    {
        var workup = ReactionWorkupCompletenessRule.Evaluate(
            r.ClericalCheckCompleted, r.VisualInspectionCompleted, r.DatResult, r.ElutionResult);
        return new(
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
            testsIncompleteAtIssue);
    }
}

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
        IRepository<ProductType>? productTypes = null)
    {
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
    }

    public Task<IReadOnlyList<ReactionInvestigation>> ListAsync(CancellationToken ct = default) =>
        _investigations.ListAsync(ct);

    public Task<ReactionInvestigation?> GetAsync(long id, CancellationToken ct = default) =>
        _investigations.GetByIdAsync(id, ct);

    public async Task<IReadOnlyList<ReactionInvestigationDto>> ListDtosAsync(CancellationToken ct = default)
    {
        var rows = await _investigations.ListAsync(ct);
        var context = await LoadDisplayContextAsync(rows, ct);
        return rows
            .OrderByDescending(r => r.ReportedUtc)
            .Select(r => context.ToDto(r))
            .ToList();
    }

    public async Task<ReactionInvestigationDto?> GetDtoAsync(long id, CancellationToken ct = default)
    {
        var row = await _investigations.GetByIdAsync(id, ct);
        if (row is null)
        {
            return null;
        }

        var context = await LoadDisplayContextAsync([row], ct);
        return context.ToDto(row);
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

        if (request.RemainderQuarantined == true && !row.RemainderQuarantined)
        {
            await TryQuarantineRemainderAsync(row, ct);
            if (!row.RemainderQuarantined)
            {
                return OperationResult<ReactionInvestigation>.Fail(
                    "Remainder / segments could not be moved to quality quarantine. Hold the bag in inventory before marking remainder held.");
            }
        }

        if (request.IsFatality == true && !row.IsFatality)
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

            var workup = ReactionWorkupCompletenessRule.Evaluate(
                row.ClericalCheckCompleted,
                row.VisualInspectionCompleted,
                row.DatResult,
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
        return OperationResult<ReactionInvestigation>.Ok(row);
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

        var oldStatus = row.FatalityNotificationStatus;
        var oldNotified = row.CberNotifiedUtc;
        row.CberNotifiedUtc = _clock.UtcNow;
        row.FatalityNotificationStatus = FatalityNotificationStatus.CberNotified;
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

        var oldStatus = row.FatalityNotificationStatus;
        var oldSubmitted = row.WrittenReportSubmittedUtc;
        row.WrittenReportSubmittedUtc = _clock.UtcNow;
        row.FatalityNotificationStatus = FatalityNotificationStatus.WrittenReportSubmitted;
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
        row.RepeatPatientAboRh,
        row.RepeatUnitAboRh,
        row.DatResult,
        row.ElutionResult,
        row.ClericalCheckCompleted,
        row.VisualInspectionCompleted,
        row.RemainderQuarantined,
        row.IsFatality,
        row.ProductAtFault
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

    private async Task TryQuarantineRemainderAsync(ReactionInvestigation row, CancellationToken ct)
    {
        var unit = await _inventory.GetUnitAsync(row.BloodProductId, ct);
        if (unit is null)
            return;

        if (unit.Status == UnitStatus.Quarantine)
        {
            row.RemainderQuarantined = true;
            if (unit.QuarantineReasonCode == UnitQuarantineReason.Unspecified
                || unit.QuarantineReasonCode == UnitQuarantineReason.Other)
            {
                unit.QuarantineReasonCode = UnitQuarantineReason.ReactionRemainder;
            }

            return;
        }

        if (!CanHoldReactionRemainder(unit.Status))
            return;

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

        var transfusionIds = rows.Select(r => r.TransfusionEventId).Distinct().ToList();
        var transfusions = transfusionIds.Count == 0
            ? []
            : await _transfusions.ListAsync(t => transfusionIds.Contains(t.Id), ct);
        var issueIds = transfusions.Select(t => t.IssueId).Distinct().ToList();
        var issues = _issues is null || issueIds.Count == 0
            ? []
            : await _issues.ListAsync(i => issueIds.Contains(i.Id), ct);

        return new ReactionDisplayContext(patients, types, antibodies, units, products, transfusions, issues);
    }

    private sealed class ReactionDisplayContext(
        IReadOnlyList<Patient> patients,
        IReadOnlyList<PatientBloodTypeHistory> types,
        IReadOnlyList<AntibodyHistory> antibodies,
        IReadOnlyDictionary<long, BloodUnit> units,
        IReadOnlyList<ProductType> products,
        IReadOnlyList<TransfusionEvent> transfusions,
        IReadOnlyList<Issue> issues)
    {
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

            var (incompatible, alert) = EvaluateLabeledCompatibility(type, unit, product);
            return ReactionInvestigationDto.From(
                row,
                patient?.MedicalRecordNumber,
                patient is null ? null : $"{patient.LastName}, {patient.FirstName}",
                unit?.UnitNumber,
                type?.BloodType.ToString(),
                history is { Count: > 0 },
                FormatAntibodySummary(history),
                unit is null ? null : new AboRh(unit.Abo, unit.RhD).ToString(),
                incompatible,
                alert,
                issue?.IssueType,
                issue?.CrossmatchStatus,
                issue?.TestsIncompleteAtIssue ?? false);
        }

        private static (bool Incompatible, string? Alert) EvaluateLabeledCompatibility(
            PatientBloodTypeHistory? type,
            BloodUnit? unit,
            ProductType? product)
        {
            if (type is null || unit is null)
            {
                return (false, null);
            }

            var component = product?.ComponentClass ?? ComponentClass.RedBloodCells;
            var firstStop = AboCompatibilityRule.Evaluate(type.BloodType, new AboRh(unit.Abo, unit.RhD), component)
                .FirstOrDefault(r => r.Severity == RuleSeverity.HardStop);
            return firstStop is null ? (false, null) : (true, firstStop.Message);
        }

        private static string? FormatAntibodySummary(IReadOnlyList<AntibodyHistory>? history)
        {
            if (history is not { Count: > 0 })
            {
                return null;
            }

            return string.Join(", ", history.Select(a =>
                a.IsActive
                    ? a.AntibodySpecificity
                    : $"{a.AntibodySpecificity} (historical / currently undetectable)"));
        }
    }
}
