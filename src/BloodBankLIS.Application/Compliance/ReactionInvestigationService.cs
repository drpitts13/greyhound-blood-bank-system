using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Common;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

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
    bool RemainderQuarantined = false)
{
    public static ReactionInvestigationDto From(ReactionInvestigation r) => new(
        r.Id, r.TransfusionEventId, r.PatientId, r.BloodProductId, r.ReportedUtc, r.ReportedBy,
        r.ReactionType, r.Severity, r.Findings, r.Conclusions, r.FollowUp, r.Status, r.Disposition,
        r.ProductAtFault, r.IsFatality, r.FatalityNotificationStatus, r.WrittenReportDueUtc,
        r.CberNotifiedUtc, r.WrittenReportSubmittedUtc,
        r.ClericalCheckCompleted, r.ClericalCheckNotes, r.VisualInspectionCompleted,
        r.VisualInspectionAcceptable, r.RepeatPatientAboRh, r.RepeatUnitAboRh,
        r.DatResult, r.ElutionResult, r.RemainderQuarantined);
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

    public ReactionInvestigationService(
        IRepository<ReactionInvestigation> investigations,
        IRepository<TransfusionEvent> transfusions,
        IInventoryRepository inventory,
        IUnitOfWork unitOfWork,
        IClock clock,
        ICurrentUser currentUser,
        IAuditWriter audit)
    {
        _investigations = investigations;
        _transfusions = transfusions;
        _inventory = inventory;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _currentUser = currentUser;
        _audit = audit;
    }

    public Task<IReadOnlyList<ReactionInvestigation>> ListAsync(CancellationToken ct = default) =>
        _investigations.ListAsync(ct);

    public Task<ReactionInvestigation?> GetAsync(long id, CancellationToken ct = default) =>
        _investigations.GetByIdAsync(id, ct);

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
        _audit.Record(AuditEventType.ReactionInvestigation, nameof(ReactionInvestigation), null, newValue: new { transfusion.Id }, reason: "Reaction suspected");
        return row;
    }

    public async Task<OperationResult<ReactionInvestigation>> UpdateAsync(
        long id, UpdateReactionInvestigationRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var row = await _investigations.GetByIdAsync(id, ct);
        if (row is null)
        {
            return OperationResult<ReactionInvestigation>.Fail("Investigation not found.");
        }

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
                row.RemainderQuarantined = true;
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

        _audit.Record(AuditEventType.ReactionInvestigation, nameof(ReactionInvestigation), id, reason: "Investigation updated");
        await _unitOfWork.SaveChangesAsync(ct);
        return OperationResult<ReactionInvestigation>.Ok(row);
    }

    public async Task<OperationResult<ReactionInvestigation>> RecordCberNotificationAsync(long id, CancellationToken ct = default)
    {
        var row = await _investigations.GetByIdAsync(id, ct);
        if (row is null)
        {
            return OperationResult<ReactionInvestigation>.Fail("Investigation not found.");
        }

        row.CberNotifiedUtc = _clock.UtcNow;
        row.FatalityNotificationStatus = FatalityNotificationStatus.CberNotified;
        await _unitOfWork.SaveChangesAsync(ct);
        return OperationResult<ReactionInvestigation>.Ok(row);
    }

    public async Task<OperationResult<ReactionInvestigation>> RecordWrittenReportAsync(long id, CancellationToken ct = default)
    {
        var row = await _investigations.GetByIdAsync(id, ct);
        if (row is null)
        {
            return OperationResult<ReactionInvestigation>.Fail("Investigation not found.");
        }

        row.WrittenReportSubmittedUtc = _clock.UtcNow;
        row.FatalityNotificationStatus = FatalityNotificationStatus.WrittenReportSubmitted;
        await _unitOfWork.SaveChangesAsync(ct);
        return OperationResult<ReactionInvestigation>.Ok(row);
    }

    private async Task TryQuarantineRemainderAsync(ReactionInvestigation row, CancellationToken ct)
    {
        var unit = await _inventory.GetUnitAsync(row.BloodProductId, ct);
        if (unit is null)
            return;

        if (!InventoryStatusTransition.IsAllowed(unit.Status, UnitStatus.Quarantine))
            return;

        const string reason = "Transfusion reaction investigation — remainder or segments held.";
        var from = unit.Status;
        unit.Status = UnitStatus.Quarantine;
        unit.QuarantineReason = reason;
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
}
