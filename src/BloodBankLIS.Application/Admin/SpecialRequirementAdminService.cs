using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Common;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Domain.Rules.Config;

namespace BloodBankLIS.Application.Admin;

public sealed class SpecialRequirementAdminService : ConfigAdminServiceBase
{
    private const string EntityType = nameof(SpecialRequirementDefinition);

    private readonly IRepository<SpecialRequirementDefinition> _repo;
    private readonly IRepository<ProductAttribute> _productAttributes;
    private readonly IPermissionEvaluator? _permissionEvaluator;

    public SpecialRequirementAdminService(
        IRepository<SpecialRequirementDefinition> repo,
        IRepository<ProductAttribute> productAttributes,
        IUnitOfWork unitOfWork,
        IClock clock,
        ICurrentUser currentUser,
        IAuditWriter audit,
        IConfigurationHistoryWriter history,
        IPermissionEvaluator? permissionEvaluator = null)
        : base(unitOfWork, clock, currentUser, audit, history)
    {
        _repo = repo;
        _productAttributes = productAttributes;
        _permissionEvaluator = permissionEvaluator;
    }

    public async Task<IReadOnlyList<SpecialRequirementDefinitionDto>> ListAsync(bool includeInactive, CancellationToken ct = default)
    {
        var items = includeInactive
            ? await _repo.ListAsync(ct)
            : await _repo.ListAsync(d => d.IsActive, ct);
        return items.OrderBy(d => d.Level).ThenBy(d => d.SortOrder).ThenBy(d => d.Code)
            .Select(SpecialRequirementDefinitionDtoMapping.From).ToList();
    }

    public async Task<SpecialRequirementDefinitionDto?> GetAsync(long id, CancellationToken ct = default)
    {
        var item = await _repo.GetByIdAsync(id, ct);
        return item is null ? null : SpecialRequirementDefinitionDtoMapping.From(item);
    }

    public async Task<EvaluationResult<SpecialRequirementDefinitionDto>> CreateAsync(
        SaveSpecialRequirementDefinitionRequest req, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(req);

        var denied = await RejectUnauthorizedEvalAsync(
            PermissionCodes.AdminConfigEdit, SpecialRequirementAuthorizationRule.EvaluateCreate, ct);
        if (denied is not null)
        {
            return denied;
        }

        var entity = new SpecialRequirementDefinition { IsDraft = true, IsActive = false, Version = 1 };
        Apply(entity, req);

        var evaluation = await ValidateAsync(entity, 0, ct);
        if (evaluation.IsHardStopped)
        {
            return EvaluationResult<SpecialRequirementDefinitionDto>.Blocked(evaluation);
        }

        await _repo.AddAsync(entity, ct);
        await UnitOfWork.SaveChangesAsync(ct);

        RecordChange(EntityType, entity.Id, entity.Version, ConfigChangeAction.Create, AuditEventType.TestChange,
            oldValue: null, newValue: SpecialRequirementDefinitionDtoMapping.From(entity), reason: req.ChangeReason);
        await UnitOfWork.SaveChangesAsync(ct);

        return EvaluationResult<SpecialRequirementDefinitionDto>.Ok(SpecialRequirementDefinitionDtoMapping.From(entity), evaluation);
    }

    public async Task<EvaluationResult<SpecialRequirementDefinitionDto>> UpdateAsync(
        long id, SaveSpecialRequirementDefinitionRequest req, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(req);

        var denied = await RejectUnauthorizedEvalAsync(
            PermissionCodes.AdminConfigEdit, SpecialRequirementAuthorizationRule.EvaluateUpdate, ct);
        if (denied is not null)
        {
            return denied;
        }

        var entity = await _repo.GetByIdAsync(id, ct);
        if (entity is null)
        {
            return EvaluationResult<SpecialRequirementDefinitionDto>.Fail("Special requirement definition not found.");
        }

        if (entity.IsActive && string.IsNullOrWhiteSpace(req.ChangeReason))
        {
            return EvaluationResult<SpecialRequirementDefinitionDto>.Fail("A change reason is required to edit an active special requirement definition.");
        }

        var before = SpecialRequirementDefinitionDtoMapping.From(entity);
        Apply(entity, req);
        if (entity.IsActive)
        {
            entity.Version += 1;
        }

        var evaluation = await ValidateAsync(entity, entity.Id, ct);
        if (evaluation.IsHardStopped)
        {
            return EvaluationResult<SpecialRequirementDefinitionDto>.Blocked(evaluation);
        }

        _repo.Update(entity);
        RecordChange(EntityType, entity.Id, entity.Version, ConfigChangeAction.Update, AuditEventType.TestChange,
            oldValue: before, newValue: SpecialRequirementDefinitionDtoMapping.From(entity), reason: req.ChangeReason);
        await UnitOfWork.SaveChangesAsync(ct);

        return EvaluationResult<SpecialRequirementDefinitionDto>.Ok(SpecialRequirementDefinitionDtoMapping.From(entity), evaluation);
    }

    public async Task<EvaluationResult<SpecialRequirementDefinitionDto>> ActivateAsync(
        long id, string? reason, CancellationToken ct = default)
    {
        var denied = await RejectUnauthorizedEvalAsync(
            PermissionCodes.AdminConfigActivate, SpecialRequirementAuthorizationRule.EvaluateActivate, ct);
        if (denied is not null)
        {
            return denied;
        }

        var entity = await _repo.GetByIdAsync(id, ct);
        if (entity is null)
        {
            return EvaluationResult<SpecialRequirementDefinitionDto>.Fail("Special requirement definition not found.");
        }

        var evaluation = await ValidateAsync(entity, entity.Id, ct);
        if (evaluation.IsHardStopped)
        {
            return EvaluationResult<SpecialRequirementDefinitionDto>.Blocked(evaluation);
        }

        entity.IsActive = true;
        entity.IsDraft = false;
        entity.RetiredUtc = null;
        entity.EffectiveUtc ??= Clock.UtcNow;
        entity.ChangeReason = reason;
        _repo.Update(entity);

        RecordChange(EntityType, entity.Id, entity.Version, ConfigChangeAction.Activate, AuditEventType.Activate,
            oldValue: null, newValue: SpecialRequirementDefinitionDtoMapping.From(entity), reason: reason);
        await UnitOfWork.SaveChangesAsync(ct);

        return EvaluationResult<SpecialRequirementDefinitionDto>.Ok(SpecialRequirementDefinitionDtoMapping.From(entity), evaluation);
    }

    public async Task<OperationResult<SpecialRequirementDefinitionDto>> DeactivateAsync(
        long id, string? reason, CancellationToken ct = default)
    {
        var denied = await RejectUnauthorizedAsync(
            PermissionCodes.AdminConfigActivate, SpecialRequirementAuthorizationRule.EvaluateDeactivate, ct);
        if (denied is not null)
        {
            return denied;
        }

        var entity = await _repo.GetByIdAsync(id, ct);
        if (entity is null)
        {
            return OperationResult<SpecialRequirementDefinitionDto>.Fail("Special requirement definition not found.");
        }

        entity.IsActive = false;
        entity.RetiredUtc = Clock.UtcNow;
        entity.ChangeReason = reason;
        _repo.Update(entity);

        RecordChange(EntityType, entity.Id, entity.Version, ConfigChangeAction.Deactivate, AuditEventType.Deactivate,
            oldValue: null, newValue: SpecialRequirementDefinitionDtoMapping.From(entity), reason: reason);
        await UnitOfWork.SaveChangesAsync(ct);

        return OperationResult<SpecialRequirementDefinitionDto>.Ok(SpecialRequirementDefinitionDtoMapping.From(entity));
    }

    private async Task<RuleEvaluation> ValidateAsync(SpecialRequirementDefinition entity, long selfId, CancellationToken ct)
    {
        var duplicate = await HasActiveDuplicateAsync(entity.Code, selfId, ct);
        var knownAttribute = true;
        if (entity.EnforcementKind == SpecialRequirementEnforcementKind.RequireProductAttribute
            && !string.IsNullOrWhiteSpace(entity.ProductAttributeCode))
        {
            var code = entity.ProductAttributeCode.Trim();
            knownAttribute = await _productAttributes.AnyAsync(a => a.IsActive && a.Code == code, ct);
        }

        return SpecialRequirementDefinitionValidator.Validate(entity, duplicate, knownAttribute);
    }

    private async Task<bool> HasActiveDuplicateAsync(string code, long selfId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return false;
        }

        var normalized = code.Trim();
        return await _repo.AnyAsync(d => d.IsActive && d.Id != selfId && d.Code == normalized, ct);
    }

    private static void Apply(SpecialRequirementDefinition e, SaveSpecialRequirementDefinitionRequest req)
    {
        e.Code = (req.Code ?? string.Empty).Trim().ToUpperInvariant();
        e.Name = req.Name?.Trim() ?? string.Empty;
        e.Level = req.Level;
        e.EnforcementKind = req.EnforcementKind;
        e.ProductAttributeCode = string.IsNullOrWhiteSpace(req.ProductAttributeCode)
            ? null
            : req.ProductAttributeCode.Trim().ToUpperInvariant();
        e.Instruction = string.IsNullOrWhiteSpace(req.Instruction) ? null : req.Instruction.Trim();
        e.SortOrder = req.SortOrder;
        e.ChangeReason = req.ChangeReason;
    }

    private async Task<EvaluationResult<SpecialRequirementDefinitionDto>?> RejectUnauthorizedEvalAsync(
        string permissionCode,
        Func<bool, RuleResult> evaluate,
        CancellationToken ct)
    {
        if (_permissionEvaluator is null)
        {
            return null;
        }

        var allowed = await _permissionEvaluator.HasPermissionAsync(
            CurrentUser.UserName, permissionCode, ct);
        var auth = evaluate(allowed);
        return auth.Severity == RuleSeverity.HardStop
            ? EvaluationResult<SpecialRequirementDefinitionDto>.Blocked(new RuleEvaluation([auth]))
            : null;
    }

    private async Task<OperationResult<SpecialRequirementDefinitionDto>?> RejectUnauthorizedAsync(
        string permissionCode,
        Func<bool, RuleResult> evaluate,
        CancellationToken ct)
    {
        if (_permissionEvaluator is null)
        {
            return null;
        }

        var allowed = await _permissionEvaluator.HasPermissionAsync(
            CurrentUser.UserName, permissionCode, ct);
        var auth = evaluate(allowed);
        return auth.Severity == RuleSeverity.HardStop
            ? OperationResult<SpecialRequirementDefinitionDto>.Fail(auth.Message)
            : null;
    }
}
