using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Common;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Application.Immunohematology;

/// <summary>
/// Patient immunohematology history: current/historical ABO/Rh, manual ABO/Rh edits
/// (a dangerous action), and antibody history. Append-only; nothing is silently
/// removed (see docs/safety-rules.md sections 5-7).
/// </summary>
public sealed class ImmunohematologyService
{
    private readonly IRepository<PatientBloodTypeHistory> _bloodTypes;
    private readonly IRepository<AntibodyHistory> _antibodies;
    private readonly IRepository<AntigenProfile> _antigenProfiles;
    private readonly IRepository<BloodAttributeDefinition> _bloodAttributes;
    private readonly IRepository<Patient> _patients;
    private readonly IRepository<AntibodyIdentificationWorkup> _workups;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditWriter _audit;
    private readonly IPermissionEvaluator? _permissions;
    private readonly IRepository<AntibodyIdentificationFinding>? _findings;

    public ImmunohematologyService(
        IRepository<PatientBloodTypeHistory> bloodTypes,
        IRepository<AntibodyHistory> antibodies,
        IRepository<AntigenProfile> antigenProfiles,
        IRepository<BloodAttributeDefinition> bloodAttributes,
        IRepository<Patient> patients,
        IRepository<AntibodyIdentificationWorkup> workups,
        IUnitOfWork unitOfWork,
        IClock clock,
        ICurrentUser currentUser,
        IAuditWriter audit,
        IPermissionEvaluator? permissions = null,
        IRepository<AntibodyIdentificationFinding>? findings = null)
    {
        _bloodTypes = bloodTypes;
        _antibodies = antibodies;
        _antigenProfiles = antigenProfiles;
        _bloodAttributes = bloodAttributes;
        _patients = patients;
        _workups = workups;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _currentUser = currentUser;
        _audit = audit;
        _permissions = permissions;
        _findings = findings;
    }

    public Task<PatientBloodTypeHistory?> GetCurrentBloodTypeAsync(long patientId, CancellationToken ct = default) =>
        _bloodTypes.FirstOrDefaultAsync(h => h.PatientId == patientId && h.IsCurrent, ct);

    public Task<IReadOnlyList<PatientBloodTypeHistory>> GetBloodTypeHistoryAsync(long patientId, CancellationToken ct = default) =>
        _bloodTypes.ListAsync(h => h.PatientId == patientId, ct);

    public Task<IReadOnlyList<AntibodyHistory>> GetActiveAntibodiesAsync(long patientId, CancellationToken ct = default) =>
        _antibodies.ListAsync(a => a.PatientId == patientId && a.IsActive, ct);

    public Task<IReadOnlyList<AntibodyHistory>> GetAntibodyHistoryAsync(long patientId, CancellationToken ct = default) =>
        _antibodies.ListAsync(a => a.PatientId == patientId, ct);

    /// <summary>
    /// Dangerous action: manually record/override the ABO/Rh. Requires a reason and
    /// writes a named audit event in addition to appending append-only history.
    /// </summary>
    public async Task<OperationResult<PatientBloodTypeHistory>> RecordBloodTypeManualAsync(
        long patientId, AboGroup abo, RhType rhD, string reason, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return OperationResult<PatientBloodTypeHistory>.Fail("A reason is required to manually record ABO/Rh.");
        }

        var denied = await RejectUnauthorizedAsync<PatientBloodTypeHistory>(
            PermissionCodes.ImmunoOverride, ImmunoAuthorizationRule.EvaluateManualBloodType, ct);
        if (denied is not null)
        {
            return denied;
        }

        var patientGate = await RejectMergedOrMissingPatientAsync<PatientBloodTypeHistory>(patientId, ct);
        if (patientGate is not null)
        {
            return patientGate;
        }

        var current = await _bloodTypes.FirstOrDefaultAsync(h => h.PatientId == patientId && h.IsCurrent, ct);
        var typeChanged = current is null || current.Abo != abo || current.RhD != rhD;
        if (current is not null)
        {
            current.IsCurrent = false;
            _bloodTypes.Update(current);
        }

        var entry = new PatientBloodTypeHistory
        {
            PatientId = patientId,
            Abo = abo,
            RhD = rhD,
            Source = BloodTypeSource.ManualEntry,
            IsCurrent = true,
            Reason = reason
        };
        await _bloodTypes.AddAsync(entry, ct);

        _audit.Record(
            AuditEventType.Override,
            nameof(PatientBloodTypeHistory),
            patientId,
            oldValue: current is null ? null : new { current.Abo, current.RhD },
            newValue: new { entry.Abo, entry.RhD, entry.Source },
            reason: reason);

        await _unitOfWork.SaveChangesAsync(ct);

        IReadOnlyList<RuleResult>? warnings = null;
        if (typeChanged)
        {
            var bloodTypeOpen = AntibodyIdentificationHistoryPostRule.EvaluateBloodTypeOpenWorkup(
                await HasOpenWorkupAsync(patientId, ct));
            if (bloodTypeOpen.Severity == RuleSeverity.Warning)
            {
                await AntibodyIdentificationJudgmentWithdrawal.WithdrawAfterPatientTypeChangeAsync(
                    _workups,
                    _findings,
                    _audit,
                    _unitOfWork,
                    patientId,
                    "Patient ABO/Rh changed while an antibody-identification workup was open. Interpretation and supervisor review must be repeated. This does not identify antibodies.",
                    ct);
                warnings = [bloodTypeOpen];
            }
            else
            {
                var bloodTypeDone = AntibodyIdentificationHistoryPostRule.EvaluateBloodTypeCompletedWorkup(
                    await HasCompletedUnscopedWorkupAsync(patientId, ct));
                if (bloodTypeDone.Severity == RuleSeverity.Warning)
                {
                    warnings = [bloodTypeDone];
                }
            }
        }

        return OperationResult<PatientBloodTypeHistory>.Ok(entry, warnings);
    }

    public Task<IReadOnlyList<AntigenProfile>> GetAntigenProfilesAsync(long patientId, CancellationToken ct = default) =>
        _antigenProfiles.ListAsync(p => p.PatientId == patientId, ct);

    public async Task<OperationResult<AntigenProfile>> SaveAntigenProfileAsync(
        long patientId, SaveAntigenProfileRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var denied = await RejectUnauthorizedAsync<AntigenProfile>(
            PermissionCodes.ImmunoRecord, ImmunoAuthorizationRule.EvaluateAntigenProfile, ct);
        if (denied is not null)
        {
            return denied;
        }

        var patientGate = await RejectMergedOrMissingPatientAsync<AntigenProfile>(patientId, ct);
        if (patientGate is not null)
        {
            return patientGate;
        }

        var definition = await _bloodAttributes.GetByIdAsync(request.BloodAttributeDefinitionId, ct);
        if (definition is null || !definition.IsActive)
        {
            return OperationResult<AntigenProfile>.Fail("Blood attribute definition not found or inactive.");
        }

        var existing = await _antigenProfiles.FirstOrDefaultAsync(
            p => p.PatientId == patientId && p.BloodAttributeDefinitionId == request.BloodAttributeDefinitionId, ct);

        var phenotypeChanged = existing is null
            || existing.Result != request.Result
            || !string.Equals(existing.Method, request.Method, StringComparison.Ordinal);

        var previous = existing is null
            ? null
            : new { existing.Result, existing.Method, existing.TestedBy, existing.TestedUtc };

        if (existing is null)
        {
            existing = new AntigenProfile
            {
                PatientId = patientId,
                BloodAttributeDefinitionId = request.BloodAttributeDefinitionId,
                Result = request.Result,
                Method = request.Method,
                TestedUtc = _clock.UtcNow,
                TestedBy = _currentUser.UserName
            };
            await _antigenProfiles.AddAsync(existing, ct);
        }
        else
        {
            existing.Result = request.Result;
            existing.Method = request.Method;
            existing.TestedUtc = _clock.UtcNow;
            existing.TestedBy = _currentUser.UserName;
            _antigenProfiles.Update(existing);
        }

        await _unitOfWork.SaveChangesAsync(ct);

        _audit.Record(
            AuditEventType.Antibody,
            nameof(AntigenProfile),
            existing.Id,
            oldValue: previous,
            newValue: new
            {
                existing.PatientId,
                existing.BloodAttributeDefinitionId,
                existing.Result,
                existing.Method,
                existing.TestedBy
            },
            reason: previous is null
                ? "Antigen phenotype recorded."
                : "Antigen phenotype updated in place (OCD-022).");
        await _unitOfWork.SaveChangesAsync(ct);

        IReadOnlyList<RuleResult>? warnings = null;
        if (phenotypeChanged)
        {
            var antigenOpen = AntibodyIdentificationHistoryPostRule.EvaluateAntigenOpenWorkup(
                await HasOpenWorkupAsync(patientId, ct));
            if (antigenOpen.Severity == RuleSeverity.Warning)
            {
                await AntibodyIdentificationJudgmentWithdrawal.WithdrawAfterPatientTypeChangeAsync(
                    _workups,
                    _findings,
                    _audit,
                    _unitOfWork,
                    patientId,
                    "Patient antigen type changed while an antibody-identification workup was open. Interpretation and supervisor review must be repeated. This does not identify antibodies.",
                    ct);
                warnings = [antigenOpen];
            }
            else
            {
                var antigenDone = AntibodyIdentificationHistoryPostRule.EvaluateAntigenCompletedWorkup(
                    await HasCompletedUnscopedWorkupAsync(patientId, ct),
                    await PostedIdentifiedConflictsWithAntigenAsync(
                        patientId, definition.Id, request.Result, ct));
                if (antigenDone.Severity == RuleSeverity.Warning)
                {
                    warnings = [antigenDone];
                }
            }
        }

        return OperationResult<AntigenProfile>.Ok(existing, warnings);
    }

    public async Task<OperationResult<AntibodyHistory>> AddAntibodyAsync(
        long patientId, long? bloodAttributeDefinitionId, string? specificity, AntibodyStatus status, string? comment, CancellationToken ct = default)
    {
        var denied = await RejectUnauthorizedAsync<AntibodyHistory>(
            PermissionCodes.ImmunoRecord, ImmunoAuthorizationRule.EvaluateAntibodyAdd, ct);
        if (denied is not null)
        {
            return denied;
        }

        var patientGate = await RejectMergedOrMissingPatientAsync<AntibodyHistory>(patientId, ct);
        if (patientGate is not null)
        {
            return patientGate;
        }

        var hasOpenWorkup = await HasOpenWorkupAsync(patientId, ct);
        var openWorkupGate = AntibodyIdentificationHistoryPostRule.EvaluateManualHistoryAdd(hasOpenWorkup);
        if (openWorkupGate.Severity == RuleSeverity.HardStop)
        {
            return OperationResult<AntibodyHistory>.Fail(openWorkupGate.Message);
        }

        string resolvedSpecificity;
        long? definitionId = bloodAttributeDefinitionId;

        if (bloodAttributeDefinitionId is long defId)
        {
            var definition = await _bloodAttributes.GetByIdAsync(defId, ct);
            if (definition is null || !definition.IsActive)
            {
                return OperationResult<AntibodyHistory>.Fail("Blood attribute definition not found or inactive.");
            }

            resolvedSpecificity = definition.AntibodyName;
        }
        else if (!string.IsNullOrWhiteSpace(specificity))
        {
            resolvedSpecificity = specificity.Trim();
        }
        else
        {
            return OperationResult<AntibodyHistory>.Fail("A catalog antibody or free-text specificity is required.");
        }

        var antibody = new AntibodyHistory
        {
            PatientId = patientId,
            BloodAttributeDefinitionId = definitionId,
            AntibodySpecificity = resolvedSpecificity,
            Status = status,
            Comment = comment,
            IsActive = true
        };

        await _antibodies.AddAsync(antibody, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        _audit.Record(
            AuditEventType.Antibody,
            nameof(AntibodyHistory),
            antibody.Id,
            newValue: new
            {
                antibody.PatientId,
                antibody.AntibodySpecificity,
                antibody.Status,
                antibody.BloodAttributeDefinitionId
            },
            reason: string.IsNullOrWhiteSpace(comment) ? "Antibody recorded." : comment.Trim());
        await _unitOfWork.SaveChangesAsync(ct);

        return OperationResult<AntibodyHistory>.Ok(antibody);
    }

    /// <summary>Dangerous action: deactivate an antibody record. Requires a reason and is audited.</summary>
    public async Task<OperationResult<AntibodyHistory>> DeactivateAntibodyAsync(long antibodyId, string reason, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return OperationResult<AntibodyHistory>.Fail("A reason is required to deactivate an antibody record.");
        }

        var denied = await RejectUnauthorizedAsync<AntibodyHistory>(
            PermissionCodes.ImmunoOverride, ImmunoAuthorizationRule.EvaluateAntibodyDeactivate, ct);
        if (denied is not null)
        {
            return denied;
        }

        var antibody = await _antibodies.GetByIdAsync(antibodyId, ct);
        if (antibody is null)
        {
            return OperationResult<AntibodyHistory>.Fail("Antibody record not found.");
        }

        if (!antibody.IsActive)
        {
            return OperationResult<AntibodyHistory>.Fail("Antibody record is already inactive.");
        }

        var patientGate = await RejectMergedOrMissingPatientAsync<AntibodyHistory>(antibody.PatientId, ct);
        if (patientGate is not null)
        {
            return patientGate;
        }

        var hasOpenWorkup = await HasOpenWorkupAsync(antibody.PatientId, ct);
        var openWorkupGate = AntibodyIdentificationHistoryPostRule.EvaluateManualHistoryDeactivate(hasOpenWorkup);
        if (openWorkupGate.Severity == RuleSeverity.HardStop)
        {
            return OperationResult<AntibodyHistory>.Fail(openWorkupGate.Message);
        }

        var postedGate = AntibodyIdentificationHistoryPostRule.EvaluateManualHistoryDeactivatePostedWorkup(
            await WasPostedByCompletedWorkupAsync(antibody, ct));
        IReadOnlyList<RuleResult>? warnings = postedGate.Severity == RuleSeverity.Warning
            ? [postedGate]
            : null;

        antibody.IsActive = false;
        antibody.DeactivationReason = reason;
        _antibodies.Update(antibody);

        _audit.Record(
            AuditEventType.Antibody,
            nameof(AntibodyHistory),
            antibody.Id,
            oldValue: new { IsActive = true, antibody.AntibodySpecificity },
            newValue: new { IsActive = false, antibody.AntibodySpecificity },
            reason: reason);

        await _unitOfWork.SaveChangesAsync(ct);
        return OperationResult<AntibodyHistory>.Ok(antibody, warnings);
    }

    public async Task<IReadOnlyList<AntibodyDto>> ListAntibodyDtosAsync(
        long patientId,
        bool activeOnly,
        CancellationToken ct = default)
    {
        var rows = activeOnly
            ? await GetActiveAntibodiesAsync(patientId, ct)
            : await GetAntibodyHistoryAsync(patientId, ct);
        var posted = await ListPostedWorkupFindingsAsync(patientId, ct);
        return rows
            .Select(a => AntibodyDto.From(
                a,
                AntibodyIdentificationHistoryPostRule.MatchesPostedWorkupFinding(
                    a.BloodAttributeDefinitionId,
                    a.AntibodySpecificity,
                    posted)))
            .ToList();
    }

    private Task<bool> HasOpenWorkupAsync(long patientId, CancellationToken ct) =>
        _workups.AnyAsync(
            w => w.PatientId == patientId
                && (w.Status == AntibodyWorkupStatus.InProgress
                    || w.Status == AntibodyWorkupStatus.PendingInterpretation
                    || w.Status == AntibodyWorkupStatus.PendingSupervisorReview),
            ct);

    private Task<bool> HasCompletedUnscopedWorkupAsync(long patientId, CancellationToken ct) =>
        _workups.AnyAsync(
            w => w.PatientId == patientId
                && w.Status == AntibodyWorkupStatus.Completed
                && w.SpecimenId == null,
            ct);

    private async Task<bool> PostedIdentifiedConflictsWithAntigenAsync(
        long patientId, long catalogId, AntigenResult result, CancellationToken ct)
    {
        if (result != AntigenResult.Positive)
        {
            return false;
        }

        var posted = await ListPostedWorkupFindingsAsync(patientId, ct);
        return AntibodyIdentificationHistoryPostRule.MatchesPostedWorkupFinding(catalogId, "", posted);
    }

    private async Task<bool> WasPostedByCompletedWorkupAsync(AntibodyHistory antibody, CancellationToken ct)
    {
        var posted = await ListPostedWorkupFindingsAsync(antibody.PatientId, ct);
        return AntibodyIdentificationHistoryPostRule.MatchesPostedWorkupFinding(
            antibody.BloodAttributeDefinitionId,
            antibody.AntibodySpecificity,
            posted);
    }

    private async Task<IReadOnlyList<(long? CatalogId, string Specificity)>> ListPostedWorkupFindingsAsync(
        long patientId,
        CancellationToken ct)
    {
        if (_findings is null)
        {
            return [];
        }

        var completed = await _workups.ListAsync(
            w => w.PatientId == patientId && w.Status == AntibodyWorkupStatus.Completed, ct);
        if (completed.Count == 0)
        {
            return [];
        }

        var workupIds = completed.Select(w => w.Id).ToHashSet();
        var posted = await _findings.ListAsync(f => f.PostedToHistory, ct);
        return posted
            .Where(f => workupIds.Contains(f.WorkupId))
            .Select(f => (f.BloodAttributeDefinitionId, f.Specificity))
            .ToList();
    }

    private async Task<OperationResult<T>?> RejectMergedOrMissingPatientAsync<T>(long patientId, CancellationToken ct)
    {
        var patient = await _patients.GetByIdAsync(patientId, ct);
        if (patient is null)
        {
            return OperationResult<T>.Fail("Patient not found.");
        }

        var clinical = PatientMergeRule.EvaluateClinicalUse(patient.Status);
        return clinical.Severity == RuleSeverity.HardStop
            ? OperationResult<T>.Fail(clinical.Message)
            : null;
    }

    private async Task<OperationResult<T>?> RejectUnauthorizedAsync<T>(
        string permissionCode,
        Func<bool, RuleResult> evaluate,
        CancellationToken ct)
    {
        if (_permissions is null)
        {
            return null;
        }

        var allowed = await _permissions.HasPermissionAsync(_currentUser.UserName, permissionCode, ct);
        var auth = evaluate(allowed);
        return auth.Severity == RuleSeverity.HardStop
            ? OperationResult<T>.Fail(auth.Message)
            : null;
    }
}
