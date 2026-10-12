using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Common;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Application.Immunohematology;

public sealed record CreateDatElutionWorkupRequest(
    long? SpecimenId = null,
    DatWorkupResult DatResult = DatWorkupResult.NotRecorded,
    string? DatMethod = null,
    string? ElutionResult = null,
    string? ElutionNotPerformedReason = null,
    string? Comment = null);

public sealed record UpdateDatElutionWorkupRequest(
    long? SpecimenId = null,
    DatWorkupResult DatResult = DatWorkupResult.NotRecorded,
    string? DatMethod = null,
    string? ElutionResult = null,
    string? ElutionNotPerformedReason = null,
    string? Comment = null);

public sealed record VoidDatElutionWorkupRequest(string Reason);

public sealed record DatElutionWorkupDto(
    long Id,
    long PatientId,
    long? SpecimenId,
    string? SpecimenAccession,
    DatElutionWorkupStatus Status,
    DatWorkupResult DatResult,
    string? DatMethod,
    string? ElutionResult,
    string? ElutionNotPerformedReason,
    string? Comment,
    DateTime CreatedUtc,
    string CreatedBy,
    string? CompletedBy,
    DateTime? CompletedUtc,
    string? VoidedBy,
    DateTime? VoidedUtc,
    string? VoidReason)
{
    public static DatElutionWorkupDto From(DatElutionWorkup w, string? accession = null) => new(
        w.Id, w.PatientId, w.SpecimenId, accession, w.Status, w.DatResult, w.DatMethod,
        w.ElutionResult, w.ElutionNotPerformedReason, w.Comment, w.CreatedUtc, w.CreatedBy,
        w.CompletedBy, w.CompletedUtc, w.VoidedBy, w.VoidedUtc, w.VoidReason);
}

/// <summary>
/// Patient DAT/elution studies. Does not identify antibodies or post history.
/// </summary>
public sealed class DatElutionWorkupService
{
    private readonly IRepository<DatElutionWorkup> _workups;
    private readonly IRepository<Patient> _patients;
    private readonly IRepository<Specimen> _specimens;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditWriter? _audit;
    private readonly IPermissionEvaluator? _permissions;

    public DatElutionWorkupService(
        IRepository<DatElutionWorkup> workups,
        IRepository<Patient> patients,
        IRepository<Specimen> specimens,
        IUnitOfWork unitOfWork,
        IClock clock,
        ICurrentUser currentUser,
        IAuditWriter? audit = null,
        IPermissionEvaluator? permissions = null)
    {
        _workups = workups;
        _patients = patients;
        _specimens = specimens;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _currentUser = currentUser;
        _audit = audit;
        _permissions = permissions;
    }

    public async Task<IReadOnlyList<DatElutionWorkupDto>> ListForPatientAsync(long patientId, CancellationToken ct = default)
    {
        var rows = await _workups.ListAsync(w => w.PatientId == patientId, ct);
        return await MapAsync(rows, ct);
    }

    public async Task<DatElutionWorkupDto?> GetAsync(long id, CancellationToken ct = default)
    {
        var row = await _workups.GetByIdAsync(id, ct);
        if (row is null)
        {
            return null;
        }

        return (await MapAsync([row], ct)).FirstOrDefault();
    }

    public async Task<OperationResult<DatElutionWorkup>> CreateAsync(
        long patientId,
        CreateDatElutionWorkupRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var denied = await RejectUnauthorizedAsync(ct);
        if (denied is not null)
        {
            return denied;
        }

        var patient = await _patients.GetByIdAsync(patientId, ct);
        if (patient is null)
        {
            return OperationResult<DatElutionWorkup>.Fail("Patient not found.");
        }

        if (patient.Status == PatientStatus.Merged)
        {
            return OperationResult<DatElutionWorkup>.Fail("A merged patient cannot receive a DAT/elution workup.");
        }

        var specimenGate = await EvaluateSpecimenAsync(patientId, request.SpecimenId, ct);
        if (specimenGate.Severity == RuleSeverity.HardStop)
        {
            return OperationResult<DatElutionWorkup>.Fail(specimenGate.Message);
        }

        var row = new DatElutionWorkup
        {
            PatientId = patientId,
            SpecimenId = request.SpecimenId,
            DatResult = request.DatResult,
            DatMethod = Normalize(request.DatMethod),
            ElutionResult = Normalize(request.ElutionResult),
            ElutionNotPerformedReason = Normalize(request.ElutionNotPerformedReason),
            Comment = Normalize(request.Comment),
            Status = DatElutionWorkupStatus.Open
        };
        await _workups.AddAsync(row, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        _audit?.Record(
            AuditEventType.DatElution,
            nameof(DatElutionWorkup),
            row.Id,
            newValue: Snapshot(row),
            reason: "DAT/elution workup created. This does not identify antibodies.");
        await _unitOfWork.SaveChangesAsync(ct);
        return OperationResult<DatElutionWorkup>.Ok(row);
    }

    public async Task<OperationResult<DatElutionWorkup>> UpdateAsync(
        long id,
        UpdateDatElutionWorkupRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var denied = await RejectUnauthorizedAsync(ct);
        if (denied is not null)
        {
            return denied;
        }

        var row = await _workups.GetByIdAsync(id, ct);
        if (row is null)
        {
            return OperationResult<DatElutionWorkup>.Fail("DAT/elution workup not found.");
        }

        var editable = DatElutionWorkupRule.EvaluateEditable(row.Status);
        if (editable.Severity == RuleSeverity.HardStop)
        {
            return OperationResult<DatElutionWorkup>.Fail(editable.Message);
        }

        var specimenGate = await EvaluateSpecimenAsync(row.PatientId, request.SpecimenId, ct);
        if (specimenGate.Severity == RuleSeverity.HardStop)
        {
            return OperationResult<DatElutionWorkup>.Fail(specimenGate.Message);
        }

        var previous = Snapshot(row);
        row.SpecimenId = request.SpecimenId;
        row.DatResult = request.DatResult;
        row.DatMethod = Normalize(request.DatMethod);
        row.ElutionResult = Normalize(request.ElutionResult);
        row.ElutionNotPerformedReason = Normalize(request.ElutionNotPerformedReason);
        row.Comment = Normalize(request.Comment);
        _workups.Update(row);
        _audit?.Record(
            AuditEventType.DatElution,
            nameof(DatElutionWorkup),
            row.Id,
            oldValue: previous,
            newValue: Snapshot(row),
            reason: "DAT/elution workup updated. This does not identify antibodies.");
        await _unitOfWork.SaveChangesAsync(ct);
        return OperationResult<DatElutionWorkup>.Ok(row);
    }

    public async Task<OperationResult<DatElutionWorkup>> CompleteAsync(long id, CancellationToken ct = default)
    {
        var denied = await RejectUnauthorizedAsync(ct);
        if (denied is not null)
        {
            return denied;
        }

        var row = await _workups.GetByIdAsync(id, ct);
        if (row is null)
        {
            return OperationResult<DatElutionWorkup>.Fail("DAT/elution workup not found.");
        }

        var editable = DatElutionWorkupRule.EvaluateEditable(row.Status);
        if (editable.Severity == RuleSeverity.HardStop)
        {
            return OperationResult<DatElutionWorkup>.Fail(editable.Message);
        }

        var specimenGate = await EvaluateSpecimenAsync(row.PatientId, row.SpecimenId, ct);
        if (specimenGate.Severity == RuleSeverity.HardStop)
        {
            return OperationResult<DatElutionWorkup>.Fail(specimenGate.Message);
        }

        var complete = DatElutionWorkupRule.EvaluateComplete(
            row.DatResult, row.ElutionResult, row.ElutionNotPerformedReason);
        if (complete.Severity == RuleSeverity.HardStop)
        {
            return OperationResult<DatElutionWorkup>.Fail(complete.Message);
        }

        var previous = Snapshot(row);
        row.Status = DatElutionWorkupStatus.Completed;
        row.CompletedBy = _currentUser.UserName;
        row.CompletedUtc = _clock.UtcNow;
        _workups.Update(row);
        _audit?.Record(
            AuditEventType.DatElution,
            nameof(DatElutionWorkup),
            row.Id,
            oldValue: previous,
            newValue: Snapshot(row),
            reason: "DAT/elution workup completed. This does not identify antibodies.");
        await _unitOfWork.SaveChangesAsync(ct);
        return OperationResult<DatElutionWorkup>.Ok(row);
    }

    public async Task<OperationResult<DatElutionWorkup>> VoidAsync(
        long id,
        VoidDatElutionWorkupRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var denied = await RejectUnauthorizedAsync(ct);
        if (denied is not null)
        {
            return denied;
        }

        var row = await _workups.GetByIdAsync(id, ct);
        if (row is null)
        {
            return OperationResult<DatElutionWorkup>.Fail("DAT/elution workup not found.");
        }

        var status = DatElutionWorkupRule.EvaluateVoidStatus(row.Status);
        if (status.Severity == RuleSeverity.HardStop)
        {
            return OperationResult<DatElutionWorkup>.Fail(status.Message);
        }

        var reason = DatElutionWorkupRule.EvaluateVoidReason(request.Reason);
        if (reason.Severity == RuleSeverity.HardStop)
        {
            return OperationResult<DatElutionWorkup>.Fail(reason.Message);
        }

        var previous = Snapshot(row);
        row.Status = DatElutionWorkupStatus.Voided;
        row.VoidedBy = _currentUser.UserName;
        row.VoidedUtc = _clock.UtcNow;
        row.VoidReason = request.Reason.Trim();
        _workups.Update(row);
        _audit?.Record(
            AuditEventType.DatElution,
            nameof(DatElutionWorkup),
            row.Id,
            oldValue: previous,
            newValue: Snapshot(row),
            reason: $"DAT/elution workup voided: {row.VoidReason}");
        await _unitOfWork.SaveChangesAsync(ct);
        return OperationResult<DatElutionWorkup>.Ok(row);
    }

    private async Task<RuleResult> EvaluateSpecimenAsync(long patientId, long? specimenId, CancellationToken ct)
    {
        if (specimenId is null)
        {
            return DatElutionWorkupRule.EvaluateSpecimen(null, specimenLinked: false);
        }

        var specimen = await _specimens.GetByIdAsync(specimenId.Value, ct);
        if (specimen is null || specimen.PatientId != patientId)
        {
            return RuleResult.HardStop(DatElutionWorkupRule.SpecimenCode, "Specimen was not found for this patient.");
        }

        return DatElutionWorkupRule.EvaluateSpecimen(specimen.Status, specimenLinked: true);
    }

    private async Task<IReadOnlyList<DatElutionWorkupDto>> MapAsync(
        IReadOnlyList<DatElutionWorkup> rows,
        CancellationToken ct)
    {
        var specimenIds = rows.Where(r => r.SpecimenId is not null).Select(r => r.SpecimenId!.Value).Distinct().ToList();
        var specimens = specimenIds.Count == 0
            ? new Dictionary<long, Specimen>()
            : (await _specimens.ListAsync(s => specimenIds.Contains(s.Id), ct)).ToDictionary(s => s.Id);

        return rows
            .OrderByDescending(r => r.CreatedUtc)
            .ThenByDescending(r => r.Id)
            .Select(r =>
            {
                string? accession = null;
                if (r.SpecimenId is long sid && specimens.TryGetValue(sid, out var specimen))
                {
                    accession = specimen.AccessionNumber;
                }

                return DatElutionWorkupDto.From(r, accession);
            })
            .ToList();
    }

    private async Task<OperationResult<DatElutionWorkup>?> RejectUnauthorizedAsync(CancellationToken ct)
    {
        if (_permissions is null)
        {
            return null;
        }

        var allowed = await _permissions.HasPermissionAsync(
            _currentUser.UserName, PermissionCodes.ImmunoRecord, ct);
        var auth = DatElutionWorkupRule.EvaluatePermission(allowed);
        return auth.Severity == RuleSeverity.HardStop
            ? OperationResult<DatElutionWorkup>.Fail(auth.Message)
            : null;
    }

    private static object Snapshot(DatElutionWorkup w) => new
    {
        w.PatientId,
        w.SpecimenId,
        w.Status,
        w.DatResult,
        w.DatMethod,
        w.ElutionResult,
        w.ElutionNotPerformedReason,
        w.Comment,
        w.CompletedBy,
        w.CompletedUtc,
        w.VoidedBy,
        w.VoidedUtc,
        w.VoidReason
    };

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
