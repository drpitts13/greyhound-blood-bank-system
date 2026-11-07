using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Common;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Application.Immunohematology;

public sealed record RhigEvaluationDto(
    long Id,
    long PatientId,
    RhigContext Context,
    DateTime EventUtc,
    RhType InfantRh,
    FetalBleedScreen FetalBleedScreen,
    decimal? FetalBleedPercent,
    RhigDecision Decision,
    int? VialsRecommended,
    string? DecisionNote,
    string RecordedBy,
    DateTime RecordedUtc,
    string? CompletedBy,
    DateTime? CompletedUtc,
    IReadOnlyList<string> Facts)
{
    public bool IsCompleted => CompletedUtc is not null;
}

/// <summary>Creates or updates an open RhIG evaluation (null id creates one).</summary>
public sealed record SaveRhigEvaluationRequest(
    long? Id,
    RhigContext Context,
    DateTime EventUtc,
    RhType InfantRh,
    FetalBleedScreen FetalBleedScreen,
    decimal? FetalBleedPercent,
    RhigDecision Decision,
    int? VialsRecommended,
    string? DecisionNote);

/// <summary>
/// RhIG evaluations on a patient's chart (OCD-069). Recording and editing need immuno.record; completing needs
/// result.verify. Each change is audited; a completed evaluation is not edited.
/// </summary>
public sealed class RhigEvaluationService
{
    private readonly IRepository<RhigEvaluation> _evaluations;
    private readonly IRepository<Patient> _patients;
    private readonly IRepository<PatientBloodTypeHistory> _bloodTypes;
    private readonly IRepository<AntibodyHistory> _antibodies;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditWriter _audit;
    private readonly IPermissionEvaluator? _permissions;

    public RhigEvaluationService(
        IRepository<RhigEvaluation> evaluations,
        IRepository<Patient> patients,
        IRepository<PatientBloodTypeHistory> bloodTypes,
        IRepository<AntibodyHistory> antibodies,
        IUnitOfWork unitOfWork,
        IClock clock,
        ICurrentUser currentUser,
        IAuditWriter audit,
        IPermissionEvaluator? permissions = null)
    {
        _evaluations = evaluations;
        _patients = patients;
        _bloodTypes = bloodTypes;
        _antibodies = antibodies;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _currentUser = currentUser;
        _audit = audit;
        _permissions = permissions;
    }

    public async Task<IReadOnlyList<RhigEvaluationDto>> ListAsync(long patientId, CancellationToken ct = default)
    {
        var rows = await _evaluations.ListAsync(e => e.PatientId == patientId, ct);
        var (motherRh, antiD) = await PatientFactsAsync(patientId, ct);
        return rows
            .OrderByDescending(e => e.EventUtc)
            .ThenByDescending(e => e.Id)
            .Select(e => ToDto(e, motherRh, antiD))
            .ToList();
    }

    public async Task<OperationResult<RhigEvaluationDto>> SaveAsync(long patientId, SaveRhigEvaluationRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var denied = await DeniedAsync(PermissionCodes.ImmunoRecord, "Recording an RhIG evaluation requires immunohematology record permission.", ct);
        if (denied is not null)
        {
            return denied;
        }

        var patient = await _patients.GetByIdAsync(patientId, ct);
        if (patient is null || patient.Status == PatientStatus.Merged)
        {
            return OperationResult<RhigEvaluationDto>.Fail("Patient not found or merged into another chart.");
        }

        var values = RhigEvaluationRule.EvaluateValues(request.VialsRecommended, request.FetalBleedPercent);
        if (values.Severity == RuleSeverity.HardStop)
        {
            return OperationResult<RhigEvaluationDto>.Fail(values.Message);
        }

        if (request.EventUtc > _clock.UtcNow.AddMinutes(5))
        {
            return OperationResult<RhigEvaluationDto>.Fail("The event time cannot be in the future.");
        }

        RhigEvaluation row;
        object? before = null;
        if (request.Id is long id)
        {
            var existing = await _evaluations.GetByIdAsync(id, ct);
            if (existing is null || existing.PatientId != patientId)
            {
                return OperationResult<RhigEvaluationDto>.Fail("RhIG evaluation not found.");
            }

            if (existing.CompletedUtc is not null)
            {
                return OperationResult<RhigEvaluationDto>.Fail("A completed RhIG evaluation cannot be changed; record a new one.");
            }

            before = Snapshot(existing);
            row = existing;
        }
        else
        {
            row = new RhigEvaluation
            {
                PatientId = patientId,
                RecordedBy = _currentUser.UserName,
                RecordedUtc = _clock.UtcNow
            };
            await _evaluations.AddAsync(row, ct);
        }

        row.Context = request.Context;
        row.EventUtc = DateTime.SpecifyKind(request.EventUtc, DateTimeKind.Utc);
        row.InfantRh = request.InfantRh;
        row.FetalBleedScreen = request.FetalBleedScreen;
        row.FetalBleedPercent = request.FetalBleedScreen == FetalBleedScreen.Positive ? request.FetalBleedPercent : null;
        row.Decision = request.Decision;
        row.VialsRecommended = request.Decision == RhigDecision.Candidate ? request.VialsRecommended : null;
        row.DecisionNote = string.IsNullOrWhiteSpace(request.DecisionNote) ? null : request.DecisionNote.Trim();
        if (before is not null)
        {
            _evaluations.Update(row);
        }

        await _unitOfWork.SaveChangesAsync(ct);
        _audit.Record(
            before is null ? AuditEventType.Create : AuditEventType.Update,
            nameof(RhigEvaluation),
            row.Id,
            oldValue: before,
            newValue: Snapshot(row),
            reason: before is null ? "RhIG evaluation recorded." : "RhIG evaluation updated.");
        await _unitOfWork.SaveChangesAsync(ct);
        return await ResultAsync(row, ct);
    }

    public async Task<OperationResult<RhigEvaluationDto>> CompleteAsync(long patientId, long id, CancellationToken ct = default)
    {
        var denied = await DeniedAsync(PermissionCodes.ResultVerify, "Completing an RhIG evaluation requires result verification permission.", ct);
        if (denied is not null)
        {
            return denied;
        }

        var row = await _evaluations.GetByIdAsync(id, ct);
        if (row is null || row.PatientId != patientId)
        {
            return OperationResult<RhigEvaluationDto>.Fail("RhIG evaluation not found.");
        }

        if (row.CompletedUtc is not null)
        {
            return OperationResult<RhigEvaluationDto>.Fail("This RhIG evaluation is already completed.");
        }

        var complete = RhigEvaluationRule.EvaluateComplete(row.Decision, row.VialsRecommended, row.DecisionNote, row.FetalBleedScreen, row.FetalBleedPercent);
        if (complete.Severity == RuleSeverity.HardStop)
        {
            return OperationResult<RhigEvaluationDto>.Fail(complete.Message);
        }

        row.CompletedBy = _currentUser.UserName;
        row.CompletedUtc = _clock.UtcNow;
        _evaluations.Update(row);
        _audit.Record(
            AuditEventType.Update,
            nameof(RhigEvaluation),
            row.Id,
            oldValue: new { CompletedUtc = (DateTime?)null },
            newValue: Snapshot(row),
            reason: "RhIG evaluation completed.");
        await _unitOfWork.SaveChangesAsync(ct);
        return await ResultAsync(row, ct);
    }

    private async Task<OperationResult<RhigEvaluationDto>> ResultAsync(RhigEvaluation row, CancellationToken ct)
    {
        var (motherRh, antiD) = await PatientFactsAsync(row.PatientId, ct);
        return OperationResult<RhigEvaluationDto>.Ok(ToDto(row, motherRh, antiD));
    }

    private async Task<(RhType? MotherRh, bool AntiD)> PatientFactsAsync(long patientId, CancellationToken ct)
    {
        var current = await _bloodTypes.FirstOrDefaultAsync(b => b.PatientId == patientId && b.IsCurrent, ct);
        var antibodies = await _antibodies.ListAsync(a => a.PatientId == patientId, ct);
        var antiD = antibodies.Any(a => string.Equals(a.AntibodySpecificity?.Trim(), "anti-D", StringComparison.OrdinalIgnoreCase));
        return (current?.RhD, antiD);
    }

    private static RhigEvaluationDto ToDto(RhigEvaluation e, RhType? motherRh, bool antiD) =>
        new(e.Id, e.PatientId, e.Context, e.EventUtc, e.InfantRh, e.FetalBleedScreen, e.FetalBleedPercent, e.Decision,
            e.VialsRecommended, e.DecisionNote, e.RecordedBy, e.RecordedUtc, e.CompletedBy, e.CompletedUtc,
            RhigEvaluationRule.Facts(motherRh, antiD, e.InfantRh, e.FetalBleedScreen, e.FetalBleedPercent));

    private static object Snapshot(RhigEvaluation e) => new
    {
        e.PatientId,
        e.Context,
        e.EventUtc,
        e.InfantRh,
        e.FetalBleedScreen,
        e.FetalBleedPercent,
        e.Decision,
        e.VialsRecommended,
        e.DecisionNote,
        e.CompletedBy,
        e.CompletedUtc
    };

    private async Task<OperationResult<RhigEvaluationDto>?> DeniedAsync(string permission, string message, CancellationToken ct) =>
        _permissions is not null && !await _permissions.HasPermissionAsync(_currentUser.UserName, permission, ct)
            ? OperationResult<RhigEvaluationDto>.Fail(message)
            : null;
}
