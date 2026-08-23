using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Common;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Domain.Rules.Config;

namespace BloodBankLIS.Application.Admin;

/// <summary>
/// Admin management of the allowed blood-product modification paths: source product
/// code, modification type, target product code, and the expiration modification
/// code applied on execution. Validation gates activation; every change is audited
/// and snapshotted (mirrors <see cref="ProductAdminService"/>).
/// </summary>
public sealed class ModificationRuleAdminService : ConfigAdminServiceBase
{
    private const string EntityType = nameof(ModificationRule);

    private readonly IRepository<ModificationRule> _rules;
    private readonly IRepository<ProductType> _products;
    private readonly IRepository<ExpirationModificationCode> _expirationCodes;

    public ModificationRuleAdminService(
        IRepository<ModificationRule> rules,
        IRepository<ProductType> products,
        IRepository<ExpirationModificationCode> expirationCodes,
        IUnitOfWork unitOfWork,
        IClock clock,
        ICurrentUser currentUser,
        IAuditWriter audit,
        IConfigurationHistoryWriter history)
        : base(unitOfWork, clock, currentUser, audit, history)
    {
        _rules = rules;
        _products = products;
        _expirationCodes = expirationCodes;
    }

    public async Task<IReadOnlyList<ModificationRuleDto>> ListAsync(bool includeInactive, CancellationToken ct = default)
    {
        var rules = includeInactive ? await _rules.ListAsync(ct) : await _rules.ListAsync(r => r.IsActive, ct);
        var products = await _products.ListAsync(ct);
        var codes = await _expirationCodes.ListAsync(ct);
        return rules
            .OrderBy(r => r.ModificationCode)
            .ThenBy(r => r.ModificationType)
            .ThenBy(r => r.Id)
            .Select(r => Map(r, products, codes))
            .ToList();
    }

    public async Task<ModificationRuleDto?> GetAsync(long id, CancellationToken ct = default)
    {
        var r = await _rules.GetByIdAsync(id, ct);
        if (r is null)
        {
            return null;
        }

        var products = await _products.ListAsync(ct);
        var codes = await _expirationCodes.ListAsync(ct);
        return Map(r, products, codes);
    }

    /// <summary>Active rules applicable to a unit currently classified under <paramref name="productTypeId"/>.</summary>
    public async Task<IReadOnlyList<ModificationRuleDto>> ListEligibleForProductAsync(long productTypeId, CancellationToken ct = default)
    {
        var rules = await _rules.ListAsync(r => r.IsActive && r.SourceProductTypeId == productTypeId, ct);
        var products = await _products.ListAsync(ct);
        var codes = await _expirationCodes.ListAsync(ct);
        return rules.OrderBy(r => r.ModificationType).Select(r => Map(r, products, codes)).ToList();
    }

    public async Task<EvaluationResult<ModificationRuleDto>> CreateAsync(SaveModificationRuleRequest req, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(req);

        var entity = new ModificationRule { IsActive = false, Version = 1 };
        Apply(entity, req);

        var evaluation = await ValidateAsync(entity, 0, ct);
        if (evaluation.IsHardStopped)
        {
            return EvaluationResult<ModificationRuleDto>.Blocked(evaluation);
        }

        await _rules.AddAsync(entity, ct);
        await UnitOfWork.SaveChangesAsync(ct);

        var dto = await GetAsync(entity.Id, ct);
        RecordChange(EntityType, entity.Id, entity.Version, ConfigChangeAction.Create, AuditEventType.Create,
            oldValue: null, newValue: dto, reason: req.ChangeReason);
        await UnitOfWork.SaveChangesAsync(ct);

        return EvaluationResult<ModificationRuleDto>.Ok(dto!, evaluation);
    }

    public async Task<EvaluationResult<ModificationRuleDto>> UpdateAsync(long id, SaveModificationRuleRequest req, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(req);

        var entity = await _rules.GetByIdAsync(id, ct);
        if (entity is null)
        {
            return EvaluationResult<ModificationRuleDto>.Fail("Modification rule not found.");
        }

        if (entity.IsActive && string.IsNullOrWhiteSpace(req.ChangeReason))
        {
            return EvaluationResult<ModificationRuleDto>.Fail("A change reason is required to edit an active modification rule.");
        }

        var before = await GetAsync(id, ct);
        Apply(entity, req);
        if (entity.IsActive)
        {
            entity.Version += 1;
        }

        var evaluation = await ValidateAsync(entity, entity.Id, ct);
        if (evaluation.IsHardStopped)
        {
            return EvaluationResult<ModificationRuleDto>.Blocked(evaluation);
        }

        _rules.Update(entity);

        var products = await _products.ListAsync(ct);
        var codes = await _expirationCodes.ListAsync(ct);
        var after = Map(entity, products, codes);
        RecordChange(EntityType, entity.Id, entity.Version, ConfigChangeAction.Update, AuditEventType.Update,
            oldValue: before, newValue: after, reason: req.ChangeReason);
        await UnitOfWork.SaveChangesAsync(ct);

        return EvaluationResult<ModificationRuleDto>.Ok(after, evaluation);
    }

    public async Task<EvaluationResult<ModificationRuleDto>> ActivateAsync(long id, string? reason, CancellationToken ct = default)
    {
        var entity = await _rules.GetByIdAsync(id, ct);
        if (entity is null)
        {
            return EvaluationResult<ModificationRuleDto>.Fail("Modification rule not found.");
        }

        var evaluation = await ValidateAsync(entity, entity.Id, ct);
        if (evaluation.IsHardStopped)
        {
            return EvaluationResult<ModificationRuleDto>.Blocked(evaluation);
        }

        entity.IsActive = true;
        _rules.Update(entity);

        var products = await _products.ListAsync(ct);
        var codes = await _expirationCodes.ListAsync(ct);
        var dto = Map(entity, products, codes);
        RecordChange(EntityType, entity.Id, entity.Version, ConfigChangeAction.Activate, AuditEventType.Activate,
            oldValue: null, newValue: dto, reason: reason);
        await UnitOfWork.SaveChangesAsync(ct);

        return EvaluationResult<ModificationRuleDto>.Ok(dto, evaluation);
    }

    public async Task<OperationResult<ModificationRuleDto>> DeactivateAsync(long id, string? reason, CancellationToken ct = default)
    {
        var entity = await _rules.GetByIdAsync(id, ct);
        if (entity is null)
        {
            return OperationResult<ModificationRuleDto>.Fail("Modification rule not found.");
        }

        entity.IsActive = false;
        _rules.Update(entity);

        var dto = await GetAsync(id, ct);
        RecordChange(EntityType, entity.Id, entity.Version, ConfigChangeAction.Deactivate, AuditEventType.Deactivate,
            oldValue: null, newValue: dto, reason: reason);
        await UnitOfWork.SaveChangesAsync(ct);

        return OperationResult<ModificationRuleDto>.Ok(dto!);
    }

    private async Task<RuleEvaluation> ValidateAsync(ModificationRule entity, long selfId, CancellationToken ct)
    {
        var duplicate = await _rules.AnyAsync(r =>
            r.IsActive && r.Id != selfId
            && r.SourceProductTypeId == entity.SourceProductTypeId
            && r.ModificationType == entity.ModificationType
            && r.TargetProductTypeId == entity.TargetProductTypeId, ct);
        var duplicateCode = await _rules.AnyAsync(r =>
            r.Id != selfId && r.ModificationCode == entity.ModificationCode, ct);

        bool? sourceActive = entity.SourceProductTypeId > 0
            ? (await _products.ListAsync(p => p.Id == entity.SourceProductTypeId, ct)).FirstOrDefault()?.IsActive ?? false
            : null;
        bool? targetActive = entity.TargetProductTypeId > 0
            ? (await _products.ListAsync(p => p.Id == entity.TargetProductTypeId, ct)).FirstOrDefault()?.IsActive ?? false
            : null;
        bool? expCodeActive = entity.ExpirationModificationCodeId > 0
            ? (await _expirationCodes.ListAsync(c => c.Id == entity.ExpirationModificationCodeId, ct)).FirstOrDefault()?.IsActive ?? false
            : null;

        return ModificationRuleValidator.Validate(entity, duplicate, sourceActive, targetActive, expCodeActive, duplicateCode);
    }

    private static void Apply(ModificationRule e, SaveModificationRuleRequest req)
    {
        e.ModificationCode = (req.ModificationCode ?? string.Empty).Trim().ToUpperInvariant();
        e.SourceProductTypeId = req.SourceProductTypeId;
        e.ModificationType = req.ModificationType;
        e.TargetProductTypeId = req.TargetProductTypeId;
        e.ExpirationModificationCodeId = req.ExpirationModificationCodeId;
        e.Description = req.Description?.Trim();
    }

    private static ModificationRuleDto Map(
        ModificationRule r,
        IReadOnlyList<ProductType> products,
        IReadOnlyList<ExpirationModificationCode> codes)
    {
        var source = products.FirstOrDefault(p => p.Id == r.SourceProductTypeId);
        var target = products.FirstOrDefault(p => p.Id == r.TargetProductTypeId);
        var code = codes.FirstOrDefault(c => c.Id == r.ExpirationModificationCodeId);
        return new ModificationRuleDto(
            r.Id, r.ModificationCode, r.SourceProductTypeId, source?.ProductCode ?? string.Empty,
            r.ModificationType, r.TargetProductTypeId, target?.ProductCode ?? string.Empty,
            r.ExpirationModificationCodeId, code?.Code ?? string.Empty,
            code?.RelativeTo ?? ExpirationRelativeTo.ModificationDateTime,
            r.Description, r.Version, r.IsActive);
    }
}
