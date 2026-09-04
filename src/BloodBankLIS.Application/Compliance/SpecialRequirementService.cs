using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Common;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Application.Compliance;

public sealed record AddSpecialRequirementRequest(
    SpecialTransfusionRequirementType RequirementType,
    string Reason,
    string? AntigenCode = null,
    DateTime? EffectiveUtc = null,
    DateTime? ExpiresUtc = null);

public sealed record SpecialRequirementDto(
    long Id,
    long PatientId,
    SpecialTransfusionRequirementType RequirementType,
    string? AntigenCode,
    string Reason,
    DateTime EffectiveUtc,
    DateTime? ExpiresUtc,
    bool IsActive,
    string EnteredBy)
{
    public static SpecialRequirementDto From(SpecialTransfusionRequirement r) => new(
        r.Id, r.PatientId, r.RequirementType, r.AntigenCode, r.Reason, r.EffectiveUtc, r.ExpiresUtc, r.IsActive, r.EnteredBy);
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

    public SpecialRequirementService(
        IRepository<SpecialTransfusionRequirement> requirements,
        IRepository<Patient> patients,
        IUnitOfWork unitOfWork,
        IClock clock,
        ICurrentUser currentUser,
        IAuditWriter audit,
        IPermissionEvaluator? permissions = null)
    {
        _requirements = requirements;
        _patients = patients;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _currentUser = currentUser;
        _audit = audit;
        _permissions = permissions;
    }

    public async Task<IReadOnlyList<SpecialTransfusionRequirement>> ListActiveAsync(long patientId, CancellationToken ct = default)
    {
        var now = _clock.UtcNow;
        var rows = await _requirements.ListAsync(r => r.PatientId == patientId && r.IsActive, ct);
        return rows.Where(r => r.EffectiveUtc <= now && (r.ExpiresUtc is null || r.ExpiresUtc > now)).ToList();
    }

    public Task<IReadOnlyList<SpecialTransfusionRequirement>> ListAsync(long patientId, CancellationToken ct = default) =>
        _requirements.ListAsync(r => r.PatientId == patientId, ct);

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

        if (request.RequirementType == SpecialTransfusionRequirementType.AntigenNegative
            && string.IsNullOrWhiteSpace(request.AntigenCode))
        {
            return OperationResult<SpecialTransfusionRequirement>.Fail("An antigen code is required for antigen-negative requirements.");
        }

        var row = new SpecialTransfusionRequirement
        {
            PatientId = patientId,
            RequirementType = request.RequirementType,
            AntigenCode = string.IsNullOrWhiteSpace(request.AntigenCode) ? null : request.AntigenCode.Trim(),
            Reason = request.Reason.Trim(),
            EffectiveUtc = request.EffectiveUtc ?? _clock.UtcNow,
            ExpiresUtc = request.ExpiresUtc,
            IsActive = true,
            EnteredBy = _currentUser.UserName
        };
        await _requirements.AddAsync(row, ct);
        _audit.Record(AuditEventType.Create, nameof(SpecialTransfusionRequirement), null, newValue: new { patientId, request.RequirementType }, reason: request.Reason);
        await _unitOfWork.SaveChangesAsync(ct);
        return OperationResult<SpecialTransfusionRequirement>.Ok(row);
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

        row.IsActive = false;
        row.DeactivationReason = reason.Trim();
        _audit.Record(AuditEventType.Deactivate, nameof(SpecialTransfusionRequirement), id, reason: reason);
        await _unitOfWork.SaveChangesAsync(ct);
        return OperationResult<SpecialTransfusionRequirement>.Ok(row);
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
