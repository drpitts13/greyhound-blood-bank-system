using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Common;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Domain.ValueObjects;

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

/// <summary>One eluate finding as entered. An alloantibody names a catalog antibody (id or name).</summary>
public sealed record EluateFindingInput(EluateFindingKind Kind, string? Specificity, long? BloodAttributeDefinitionId = null);

/// <summary>Replaces the workup's eluate findings (allowed until the workup is verified).</summary>
public sealed record RecordEluateFindingsRequest(IReadOnlyList<EluateFindingInput> Findings);

public sealed record EluateFindingDto(
    long Id,
    EluateFindingKind Kind,
    string Specificity,
    long? BloodAttributeDefinitionId,
    long? PostedAntibodyHistoryId)
{
    public static EluateFindingDto From(DatElutionEluateFinding f) =>
        new(f.Id, f.Kind, f.Specificity, f.BloodAttributeDefinitionId, f.PostedAntibodyHistoryId);
}

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
    string? VoidReason,
    string? VerifiedBy = null,
    DateTime? VerifiedUtc = null,
    IReadOnlyList<EluateFindingDto>? EluateFindings = null)
{
    public static DatElutionWorkupDto From(
        DatElutionWorkup w,
        string? accession = null,
        IReadOnlyList<EluateFindingDto>? findings = null) => new(
        w.Id, w.PatientId, w.SpecimenId, accession, w.Status, w.DatResult, w.DatMethod,
        w.ElutionResult, w.ElutionNotPerformedReason, w.Comment, w.CreatedUtc, w.CreatedBy,
        w.CompletedBy, w.CompletedUtc, w.VoidedBy, w.VoidedUtc, w.VoidReason,
        w.VerifiedBy, w.VerifiedUtc, findings ?? []);
}

/// <summary>
/// Patient DAT/elution studies. Eluate findings are recorded as structured antibodies; a second person's
/// verification of a completed workup posts identified alloantibodies to antibody history (OCD-042).
/// Autoantibody, panreactive, and nonspecific findings are kept on the workup and never posted.
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
    private readonly IRepository<DatElutionEluateFinding>? _findings;
    private readonly IRepository<AntibodyHistory>? _antibodies;
    private readonly IRepository<BloodAttributeDefinition>? _attributes;

    public DatElutionWorkupService(
        IRepository<DatElutionWorkup> workups,
        IRepository<Patient> patients,
        IRepository<Specimen> specimens,
        IUnitOfWork unitOfWork,
        IClock clock,
        ICurrentUser currentUser,
        IAuditWriter? audit = null,
        IPermissionEvaluator? permissions = null,
        IRepository<DatElutionEluateFinding>? findings = null,
        IRepository<AntibodyHistory>? antibodies = null,
        IRepository<BloodAttributeDefinition>? attributes = null)
    {
        _workups = workups;
        _patients = patients;
        _specimens = specimens;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _currentUser = currentUser;
        _audit = audit;
        _permissions = permissions;
        _findings = findings;
        _antibodies = antibodies;
        _attributes = attributes;
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

    /// <summary>
    /// Replaces the eluate findings. Each alloantibody must resolve to a catalog antibody so that, once posted,
    /// it drives antigen-negative selection. Not allowed after verification or on a voided workup.
    /// </summary>
    public async Task<OperationResult<DatElutionWorkup>> RecordEluateFindingsAsync(
        long id,
        RecordEluateFindingsRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (_findings is null || _attributes is null)
        {
            return OperationResult<DatElutionWorkup>.Fail("Eluate findings are not available.");
        }

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

        var editable = EluateVerificationRule.EvaluateFindingsEditable(row.Status, row.VerifiedUtc is not null);
        if (editable.Severity == RuleSeverity.HardStop)
        {
            return OperationResult<DatElutionWorkup>.Fail(editable.Message);
        }

        var catalog = await LoadAntibodyCatalogAsync(ct);
        var resolved = new List<DatElutionEluateFinding>();
        foreach (var input in request.Findings ?? [])
        {
            var match = AntibodyIdentificationCatalogResolver.Resolve(input.BloodAttributeDefinitionId, input.Specificity, catalog);
            var named = string.IsNullOrWhiteSpace(input.Specificity) ? match.Specificity : input.Specificity;
            var check = EluateVerificationRule.EvaluateFinding(input.Kind, named, match.CatalogMatched);
            if (check.Severity == RuleSeverity.HardStop)
            {
                return OperationResult<DatElutionWorkup>.Fail(check.Message);
            }

            var specificity = match.CatalogMatched ? match.Specificity : (input.Specificity ?? string.Empty).Trim();
            resolved.Add(new DatElutionEluateFinding
            {
                WorkupId = row.Id,
                Kind = input.Kind,
                BloodAttributeDefinitionId = match.CatalogMatched ? match.DefinitionId : null,
                Specificity = string.IsNullOrWhiteSpace(specificity) ? input.Kind.ToString() : specificity
            });
        }

        // Replaced findings are retired, never deleted; the audit row keeps the before and after.
        var previous = await _findings.ListAsync(f => f.WorkupId == row.Id && f.IsActive, ct);
        foreach (var old in previous)
        {
            var tracked = await _findings.GetByIdAsync(old.Id, ct);
            if (tracked is not null)
            {
                tracked.IsActive = false;
                _findings.Update(tracked);
            }
        }

        foreach (var finding in resolved)
        {
            await _findings.AddAsync(finding, ct);
        }

        _audit?.Record(
            AuditEventType.DatElution,
            nameof(DatElutionWorkup),
            row.Id,
            oldValue: new { Findings = previous.Select(f => new { f.Kind, f.Specificity, f.BloodAttributeDefinitionId }).ToList() },
            newValue: new { Findings = resolved.Select(f => new { f.Kind, f.Specificity, f.BloodAttributeDefinitionId }).ToList() },
            reason: "Eluate findings recorded. Nothing posts to antibody history until the workup is verified.");
        await _unitOfWork.SaveChangesAsync(ct);
        return OperationResult<DatElutionWorkup>.Ok(row);
    }

    /// <summary>
    /// A second person verifies a completed workup. Each alloantibody finding not already on file posts to antibody
    /// history (source: eluate); other findings stay on the workup.
    /// </summary>
    public async Task<OperationResult<DatElutionWorkup>> VerifyAsync(long id, CancellationToken ct = default)
    {
        if (_findings is null || _antibodies is null)
        {
            return OperationResult<DatElutionWorkup>.Fail("Eluate verification is not available.");
        }

        if (_permissions is not null
            && !await _permissions.HasPermissionAsync(_currentUser.UserName, PermissionCodes.ImmunoOverride, ct))
        {
            return OperationResult<DatElutionWorkup>.Fail("Verifying a DAT/elution workup requires supervisor review permission.");
        }

        var row = await _workups.GetByIdAsync(id, ct);
        if (row is null)
        {
            return OperationResult<DatElutionWorkup>.Fail("DAT/elution workup not found.");
        }

        var gate = EluateVerificationRule.EvaluateVerify(row.Status, row.CompletedBy, _currentUser.UserName, row.VerifiedUtc is not null);
        if (gate.Severity == RuleSeverity.HardStop)
        {
            return OperationResult<DatElutionWorkup>.Fail(gate.Message);
        }

        var previous = Snapshot(row);
        row.VerifiedBy = _currentUser.UserName;
        row.VerifiedUtc = _clock.UtcNow;
        _workups.Update(row);

        var findings = await _findings.ListAsync(f => f.WorkupId == row.Id && f.IsActive, ct);
        var active = await _antibodies.ListAsync(a => a.PatientId == row.PatientId && a.IsActive, ct);
        var posted = new List<(long FindingId, AntibodyHistory History)>();
        foreach (var f in findings.Where(f => EluateVerificationRule.Posts(f.Kind)))
        {
            var onFile = active.Concat(posted.Select(p => p.History)).Any(a =>
                (f.BloodAttributeDefinitionId is long catalogId && a.BloodAttributeDefinitionId == catalogId)
                || string.Equals(a.AntibodySpecificity, f.Specificity, StringComparison.Ordinal));
            if (onFile)
            {
                continue;
            }

            var history = new AntibodyHistory
            {
                PatientId = row.PatientId,
                BloodAttributeDefinitionId = f.BloodAttributeDefinitionId,
                AntibodySpecificity = f.Specificity,
                Status = AntibodyStatus.Identified,
                Comment = $"Identified in eluate (DAT/elution workup {row.Id}); verified by {row.VerifiedBy}.",
                IsActive = true
            };
            await _antibodies.AddAsync(history, ct);
            posted.Add((f.Id, history));
        }

        await _unitOfWork.SaveChangesAsync(ct);
        foreach (var (findingId, history) in posted)
        {
            var tracked = await _findings.GetByIdAsync(findingId, ct);
            if (tracked is not null)
            {
                tracked.PostedAntibodyHistoryId = history.Id;
                _findings.Update(tracked);
            }

            _audit?.Record(
                AuditEventType.Antibody,
                nameof(AntibodyHistory),
                history.Id,
                newValue: new { history.PatientId, WorkupId = row.Id, history.AntibodySpecificity, history.BloodAttributeDefinitionId, Source = "Eluate" },
                reason: "Identified in eluate on a verified DAT/elution workup.");
        }

        var postedNames = posted.Select(p => p.History.AntibodySpecificity).ToList();
        _audit?.Record(
            AuditEventType.Verify,
            nameof(DatElutionWorkup),
            row.Id,
            oldValue: previous,
            newValue: new
            {
                row.VerifiedBy,
                row.VerifiedUtc,
                Posted = postedNames,
                NotPosted = findings.Where(f => !EluateVerificationRule.Posts(f.Kind)).Select(f => new { f.Kind, f.Specificity }).ToList()
            },
            reason: postedNames.Count == 0
                ? "DAT/elution workup verified. No new alloantibody was posted."
                : "DAT/elution workup verified. Posted to antibody history: " + string.Join(", ", postedNames) + ".");
        await _unitOfWork.SaveChangesAsync(ct);
        return OperationResult<DatElutionWorkup>.Ok(row);
    }

    private async Task<IReadOnlyList<AntibodyCatalogItem>> LoadAntibodyCatalogAsync(CancellationToken ct) =>
        _attributes is null
            ? []
            : (await _attributes.ListAsync(a => a.IsActive, ct))
                .Select(a => new AntibodyCatalogItem(a.Id, a.Code, a.Name, a.AntibodyName))
                .ToList();

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

        var verified = EluateVerificationRule.EvaluateVoid(row.VerifiedUtc is not null);
        if (verified.Severity == RuleSeverity.HardStop)
        {
            return OperationResult<DatElutionWorkup>.Fail(verified.Message);
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
        var workupIds = rows.Select(r => r.Id).ToList();
        var findings = _findings is null || workupIds.Count == 0
            ? []
            : await _findings.ListAsync(f => workupIds.Contains(f.WorkupId) && f.IsActive, ct);
        var findingsByWorkup = findings
            .GroupBy(f => f.WorkupId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<EluateFindingDto>)g.OrderBy(f => f.Id).Select(EluateFindingDto.From).ToList());

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

                return DatElutionWorkupDto.From(r, accession, findingsByWorkup.GetValueOrDefault(r.Id));
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
        w.VoidReason,
        w.VerifiedBy,
        w.VerifiedUtc
    };

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
