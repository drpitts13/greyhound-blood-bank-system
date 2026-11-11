using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Common;
using BloodBankLIS.Domain.Audit;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Application.Issuing;

/// <summary>
/// OCD-083 (decided 2026-11-11, option B): a transfusion the EHR reported for a unit the blood bank had recalled,
/// quarantined, or recorded as damaged or missing after it was issued, waiting for a supervisor's review.
/// </summary>
public sealed record TransfusionReportToReviewDto(
    long TransfusionId,
    long BloodUnitId,
    string UnitNumber,
    long PatientId,
    string? MedicalRecordNumber,
    UnitStatus UnitStatusAtReport,
    string? UnitReason,
    DateTime? StartUtc,
    DateTime ReportedUtc);

public sealed record ReviewTransfusionReportRequest(string? Note);

/// <summary>
/// Lists the transfusions recorded over a unit's status from an EHR report (OCD-083 option B) and records a
/// supervisor's review of each. Reviewing changes no unit or issue: it records who reviewed, when, and what was found.
/// </summary>
public sealed class TransfusionReportReviewService
{
    public const string ReviewCode = "TX-REPORT-REVIEW";
    public const int MaxNoteLength = 500;

    private readonly IRepository<TransfusionEvent> _transfusions;
    private readonly IRepository<BloodUnit> _units;
    private readonly IRepository<Patient> _patients;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditWriter _audit;
    private readonly IPermissionEvaluator? _permissions;

    public TransfusionReportReviewService(
        IRepository<TransfusionEvent> transfusions,
        IRepository<BloodUnit> units,
        IRepository<Patient> patients,
        IUnitOfWork unitOfWork,
        IClock clock,
        ICurrentUser currentUser,
        IAuditWriter audit,
        IPermissionEvaluator? permissions = null)
    {
        _transfusions = transfusions;
        _units = units;
        _patients = patients;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _currentUser = currentUser;
        _audit = audit;
        _permissions = permissions;
    }

    public async Task<IReadOnlyList<TransfusionReportToReviewDto>> ListToReviewAsync(CancellationToken ct = default)
    {
        var rows = await _transfusions.ListAsync(t => t.UnitStatusAtReport != null && t.ReportReviewedUtc == null, ct);
        var items = new List<TransfusionReportToReviewDto>(rows.Count);
        foreach (var t in rows.OrderBy(t => t.CreatedUtc))
        {
            var unit = await _units.GetByIdAsync(t.BloodProductId, ct);
            var patient = await _patients.GetByIdAsync(t.PatientId, ct);
            var status = t.UnitStatusAtReport!.Value;
            items.Add(new TransfusionReportToReviewDto(
                t.Id, t.BloodProductId, unit?.UnitNumber ?? $"#{t.BloodProductId}", t.PatientId, patient?.MedicalRecordNumber,
                status, unit is null ? null : ReasonFor(unit, status), t.StartUtc, t.CreatedUtc));
        }

        return items;
    }

    public async Task<OperationResult<TransfusionReportToReviewDto>> ReviewAsync(
        long transfusionId, ReviewTransfusionReportRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (_permissions is not null
            && !await _permissions.HasPermissionAsync(_currentUser.UserName, PermissionCodes.InventoryRelease, ct)
            && !await _permissions.HasPermissionAsync(_currentUser.UserName, PermissionCodes.OverrideApprove, ct))
        {
            return OperationResult<TransfusionReportToReviewDto>.Fail(
                "Reviewing a reported transfusion requires inventory.release or override.approve.");
        }

        var note = request.Note?.Trim();
        if (string.IsNullOrWhiteSpace(note))
        {
            return OperationResult<TransfusionReportToReviewDto>.Fail("Say what the review found.");
        }

        if (note.Length > MaxNoteLength)
        {
            return OperationResult<TransfusionReportToReviewDto>.Fail($"The review note is limited to {MaxNoteLength} characters.");
        }

        var transfusion = await _transfusions.GetByIdAsync(transfusionId, ct);
        if (transfusion is null || transfusion.UnitStatusAtReport is null)
        {
            return OperationResult<TransfusionReportToReviewDto>.Fail("No reported transfusion waits for review with that id.");
        }

        if (transfusion.ReportReviewedUtc is not null)
        {
            return OperationResult<TransfusionReportToReviewDto>.Fail(
                $"This reported transfusion was already reviewed by {transfusion.ReportReviewedBy}.");
        }

        var now = _clock.UtcNow;
        transfusion.ReportReviewedUtc = now;
        transfusion.ReportReviewedBy = _currentUser.UserName;
        transfusion.ReportReviewNote = note;
        _audit.Record(
            AuditEventType.Transfusion,
            nameof(TransfusionEvent),
            transfusion.Id,
            oldValue: new { Reviewed = false },
            newValue: new { Reviewed = true, transfusion.UnitStatusAtReport, Note = note, Code = ReviewCode },
            reason: $"Reported transfusion reviewed: {note}");
        await _unitOfWork.SaveChangesAsync(ct);

        var unit = await _units.GetByIdAsync(transfusion.BloodProductId, ct);
        var patient = await _patients.GetByIdAsync(transfusion.PatientId, ct);
        var status = transfusion.UnitStatusAtReport.Value;
        return OperationResult<TransfusionReportToReviewDto>.Ok(new TransfusionReportToReviewDto(
            transfusion.Id, transfusion.BloodProductId, unit?.UnitNumber ?? $"#{transfusion.BloodProductId}", transfusion.PatientId,
            patient?.MedicalRecordNumber, status, unit is null ? null : ReasonFor(unit, status), transfusion.StartUtc, transfusion.CreatedUtc));
    }

    private static string? ReasonFor(BloodUnit unit, UnitStatus status) => status switch
    {
        UnitStatus.Recalled => unit.RecallReason,
        UnitStatus.Quarantine => unit.QuarantineReason,
        UnitStatus.Damaged => unit.DamagedReason,
        UnitStatus.Missing => unit.MissingReason,
        _ => null
    };
}
