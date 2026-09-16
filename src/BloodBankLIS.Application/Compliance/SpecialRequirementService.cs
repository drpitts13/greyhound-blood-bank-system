using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Common;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Application.Compliance;

public sealed record AddSpecialRequirementRequest(
    SpecialTransfusionRequirementType RequirementType,
    string Reason,
    string? AntigenCode = null,
    DateTime? EffectiveUtc = null,
    DateTime? ExpiresUtc = null,
    long? RequirementDefinitionId = null);

public sealed record SpecialRequirementDto(
    long Id,
    long PatientId,
    SpecialTransfusionRequirementType RequirementType,
    string? AntigenCode,
    string Reason,
    DateTime EffectiveUtc,
    DateTime? ExpiresUtc,
    bool IsActive,
    string EnteredBy,
    long? RequirementDefinitionId = null,
    string? Code = null,
    string? Name = null,
    SpecialRequirementLevel? Level = null,
    SpecialRequirementEnforcementKind? EnforcementKind = null,
    string? Instruction = null,
    bool IsClinicallyActive = false)
{
    public static SpecialRequirementDto From(
        SpecialTransfusionRequirement r,
        SpecialRequirementDefinition? definition = null,
        DateTime? nowUtc = null)
    {
        var now = nowUtc ?? DateTime.UtcNow;
        var code = definition?.Code ?? SpecialRequirementCatalog.CodeFor(r.RequirementType);
        var clinicallyActive = SpecialRequirementCatalog.IsClinicallyActive(
            r.IsActive, r.EffectiveUtc, r.ExpiresUtc, now);
        return new(
            r.Id,
            r.PatientId,
            r.RequirementType,
            r.AntigenCode,
            r.Reason,
            r.EffectiveUtc,
            r.ExpiresUtc,
            r.IsActive,
            r.EnteredBy,
            r.RequirementDefinitionId ?? definition?.Id,
            code,
            definition?.Name ?? r.RequirementType.ToString(),
            definition?.Level,
            definition?.EnforcementKind,
            definition?.Instruction,
            clinicallyActive);
    }
}

public sealed class SpecialRequirementService
{
    private readonly IRepository<SpecialTransfusionRequirement> _requirements;
    private readonly IRepository<Patient> _patients;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditWriter _audit;
    private readonly IPermissionEvaluator? _permissions;
    private readonly IRepository<AntibodyIdentificationWorkup>? _workups;
    private readonly IRepository<SpecialRequirementDefinition>? _definitions;

    public SpecialRequirementService(
        IRepository<SpecialTransfusionRequirement> requirements,
        IRepository<Patient> patients,
        IUnitOfWork unitOfWork,
        IClock clock,
        ICurrentUser currentUser,
        IAuditWriter audit,
        IPermissionEvaluator? permissions = null,
        IRepository<AntibodyIdentificationWorkup>? workups = null,
        IRepository<SpecialRequirementDefinition>? definitions = null)
    {
        _requirements = requirements;
        _patients = patients;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _currentUser = currentUser;
        _audit = audit;
        _permissions = permissions;
        _workups = workups;
        _definitions = definitions;
    }

    public async Task<IReadOnlyList<SpecialTransfusionRequirement>> ListActiveAsync(long patientId, CancellationToken ct = default)
    {
        var now = _clock.UtcNow;
        var rows = await _requirements.ListAsync(r => r.PatientId == patientId && r.IsActive, ct);
        return rows.Where(r => SpecialRequirementCatalog.IsClinicallyActive(r.IsActive, r.EffectiveUtc, r.ExpiresUtc, now)).ToList();
    }

    public Task<IReadOnlyList<SpecialTransfusionRequirement>> ListAsync(long patientId, CancellationToken ct = default) =>
        _requirements.ListAsync(r => r.PatientId == patientId, ct);

    public async Task<IReadOnlyList<SpecialRequirementDto>> ListDtosAsync(long patientId, CancellationToken ct = default)
    {
        var now = _clock.UtcNow;
        var rows = await _requirements.ListAsync(r => r.PatientId == patientId, ct);
        var definitions = await LoadDefinitionsAsync(ct);
        return rows
            .OrderByDescending(r => SpecialRequirementCatalog.IsClinicallyActive(r.IsActive, r.EffectiveUtc, r.ExpiresUtc, now))
            .ThenBy(r => r.EffectiveUtc)
            .Select(r => SpecialRequirementDto.From(r, ResolveDefinition(r, definitions), now))
            .ToList();
    }

    public async Task<OperationResult<SpecialTransfusionRequirement>> AddAsync(
        long patientId, AddSpecialRequirementRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return OperationResult<SpecialTransfusionRequirement>.Fail("A reason is required.");
        }

        var denied = await RejectUnauthorizedAsync(
            PermissionCodes.ImmunoRecord, ImmunoAuthorizationRule.EvaluateSpecialRequirementAdd, ct);
        if (denied is not null)
        {
            return denied;
        }

        var patient = await _patients.GetByIdAsync(patientId, ct);
        if (patient is null)
        {
            return OperationResult<SpecialTransfusionRequirement>.Fail("Patient not found.");
        }

        var clinical = PatientMergeRule.EvaluateClinicalUse(patient.Status);
        if (clinical.Severity == RuleSeverity.HardStop)
        {
            return OperationResult<SpecialTransfusionRequirement>.Fail(clinical.Message);
        }

        var definition = await ResolveDefinitionAsync(request, ct);
        var requirementType = definition is not null
            ? SpecialRequirementCatalog.TypeFor(definition.Code) ?? request.RequirementType
            : request.RequirementType;
        var enforcement = definition?.EnforcementKind
            ?? SpecialRequirementCatalog.ToRef(requirementType, request.AntigenCode, _clock.UtcNow, null, true).EnforcementKind;

        if (enforcement == SpecialRequirementEnforcementKind.RequireAntigenNegative
            && string.IsNullOrWhiteSpace(request.AntigenCode))
        {
            return OperationResult<SpecialTransfusionRequirement>.Fail("An antigen code is required for antigen-negative requirements.");
        }

        var row = new SpecialTransfusionRequirement
        {
            PatientId = patientId,
            RequirementDefinitionId = definition?.Id,
            RequirementType = requirementType,
            AntigenCode = string.IsNullOrWhiteSpace(request.AntigenCode) ? null : request.AntigenCode.Trim(),
            Reason = request.Reason.Trim(),
            EffectiveUtc = request.EffectiveUtc ?? _clock.UtcNow,
            ExpiresUtc = request.ExpiresUtc,
            IsActive = true,
            EnteredBy = _currentUser.UserName
        };
        await _requirements.AddAsync(row, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        _audit.Record(
            AuditEventType.Antibody,
            nameof(SpecialTransfusionRequirement),
            row.Id,
            newValue: new { row.PatientId, row.RequirementType, row.RequirementDefinitionId, row.AntigenCode, row.IsActive, row.ExpiresUtc },
            reason: request.Reason);
        await _unitOfWork.SaveChangesAsync(ct);
        return OperationResult<SpecialTransfusionRequirement>.Ok(row, await EvaluateOpenWorkupWarningsAsync(patientId, ct));
    }

    public async Task<OperationResult<SpecialTransfusionRequirement>> DeactivateAsync(long id, string reason, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return OperationResult<SpecialTransfusionRequirement>.Fail("A reason is required to deactivate a special requirement.");
        }

        var denied = await RejectUnauthorizedAsync(
            PermissionCodes.ImmunoOverride, ImmunoAuthorizationRule.EvaluateSpecialRequirementDeactivate, ct);
        if (denied is not null)
        {
            return denied;
        }

        var row = await _requirements.GetByIdAsync(id, ct);
        if (row is null)
        {
            return OperationResult<SpecialTransfusionRequirement>.Fail("Special requirement not found.");
        }

        var patient = await _patients.GetByIdAsync(row.PatientId, ct);
        if (patient is not null)
        {
            var clinical = PatientMergeRule.EvaluateClinicalUse(patient.Status);
            if (clinical.Severity == RuleSeverity.HardStop)
            {
                return OperationResult<SpecialTransfusionRequirement>.Fail(clinical.Message);
            }
        }

        var now = _clock.UtcNow;
        var old = new { row.PatientId, row.RequirementType, row.AntigenCode, row.IsActive, row.ExpiresUtc };
        row.IsActive = false;
        if (row.ExpiresUtc is null || row.ExpiresUtc > now)
        {
            row.ExpiresUtc = now;
        }

        row.DeactivationReason = reason.Trim();
        _audit.Record(
            AuditEventType.Deactivate,
            nameof(SpecialTransfusionRequirement),
            id,
            oldValue: old,
            newValue: new { row.PatientId, row.RequirementType, row.AntigenCode, row.IsActive, row.ExpiresUtc, row.DeactivationReason },
            reason: reason);
        await _unitOfWork.SaveChangesAsync(ct);
        return OperationResult<SpecialTransfusionRequirement>.Ok(row, await EvaluateOpenWorkupWarningsAsync(row.PatientId, ct));
    }

    private async Task<IReadOnlyList<SpecialRequirementDefinition>> LoadDefinitionsAsync(CancellationToken ct)
    {
        if (_definitions is null)
        {
            return [];
        }

        return await _definitions.ListAsync(ct);
    }

    private static SpecialRequirementDefinition? ResolveDefinition(
        SpecialTransfusionRequirement row,
        IReadOnlyList<SpecialRequirementDefinition> definitions)
    {
        if (row.RequirementDefinitionId is long id)
        {
            return definitions.FirstOrDefault(d => d.Id == id);
        }

        var code = SpecialRequirementCatalog.CodeFor(row.RequirementType);
        return definitions.FirstOrDefault(d => string.Equals(d.Code, code, StringComparison.OrdinalIgnoreCase));
    }

    private async Task<SpecialRequirementDefinition?> ResolveDefinitionAsync(
        AddSpecialRequirementRequest request, CancellationToken ct)
    {
        if (_definitions is null)
        {
            return null;
        }

        if (request.RequirementDefinitionId is long id)
        {
            return await _definitions.GetByIdAsync(id, ct);
        }

        var code = SpecialRequirementCatalog.CodeFor(request.RequirementType);
        return await _definitions.FirstOrDefaultAsync(
            d => d.IsActive && !d.IsDraft && d.Code == code, ct);
    }

    private async Task<IReadOnlyList<RuleResult>?> EvaluateOpenWorkupWarningsAsync(long patientId, CancellationToken ct)
    {
        var hasOpenWorkup = _workups is not null && await _workups.AnyAsync(
            w => w.PatientId == patientId
                && (w.Status == AntibodyWorkupStatus.InProgress
                    || w.Status == AntibodyWorkupStatus.PendingInterpretation
                    || w.Status == AntibodyWorkupStatus.PendingSupervisorReview),
            ct);
        var open = AntibodyIdentificationHistoryPostRule.EvaluateSpecialRequirementOpenWorkup(hasOpenWorkup);
        return open.Severity == RuleSeverity.Warning ? [open] : null;
    }

    private async Task<OperationResult<SpecialTransfusionRequirement>?> RejectUnauthorizedAsync(
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
            ? OperationResult<SpecialTransfusionRequirement>.Fail(auth.Message)
            : null;
    }
}
