using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Common;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Domain.Rules.Config;

namespace BloodBankLIS.Application.Admin;

/// <summary>
/// Admin management of product definitions (extends <see cref="ProductType"/>) plus the
/// product-attribute catalog and per-product attribute assignments. Validation gates
/// activation; every change is audited and snapshotted.
/// </summary>
public sealed class ProductAdminService : ConfigAdminServiceBase
{
    private const string EntityType = nameof(ProductType);

    private readonly IRepository<ProductType> _products;
    private readonly IRepository<ProductAttribute> _attributes;
    private readonly IRepository<ProductAttributeAssignment> _assignments;
    private readonly IPermissionEvaluator? _permissionEvaluator;

    public ProductAdminService(
        IRepository<ProductType> products,
        IRepository<ProductAttribute> attributes,
        IRepository<ProductAttributeAssignment> assignments,
        IUnitOfWork unitOfWork,
        IClock clock,
        ICurrentUser currentUser,
        IAuditWriter audit,
        IConfigurationHistoryWriter history,
        IPermissionEvaluator? permissionEvaluator = null)
        : base(unitOfWork, clock, currentUser, audit, history)
    {
        _products = products;
        _attributes = attributes;
        _assignments = assignments;
        _permissionEvaluator = permissionEvaluator;
    }

    public async Task<IReadOnlyList<ProductAttributeDto>> ListAttributesAsync(CancellationToken ct = default)
    {
        var items = await _attributes.ListAsync(ct);
        return items.OrderBy(a => a.Code).Select(a => new ProductAttributeDto(a.Id, a.Code, a.Name, a.Description, a.IsActive)).ToList();
    }

    public async Task<IReadOnlyList<ProductDefinitionDto>> ListAsync(bool includeInactive, CancellationToken ct = default)
    {
        var products = includeInactive ? await _products.ListAsync(ct) : await _products.ListAsync(p => p.IsActive, ct);
        var attrs = await _attributes.ListAsync(ct);
        var assigns = await _assignments.ListAsync(a => a.IsActive, ct);
        return products.OrderBy(p => p.ProductCode).Select(p => Map(p, assigns, attrs)).ToList();
    }

    public async Task<ProductDefinitionDto?> GetAsync(long id, CancellationToken ct = default)
    {
        var p = await _products.GetByIdAsync(id, ct);
        if (p is null)
        {
            return null;
        }

        var attrs = await _attributes.ListAsync(ct);
        var assigns = await _assignments.ListAsync(a => a.ProductTypeId == id && a.IsActive, ct);
        return Map(p, assigns, attrs);
    }

    public async Task<EvaluationResult<ProductDefinitionDto>> CreateAsync(SaveProductDefinitionRequest req, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(req);

        var denied = await RejectUnauthorizedEvalAsync(
            PermissionCodes.AdminProductsManage, ProductCatalogAuthorizationRule.EvaluateCreate, ct);
        if (denied is not null)
        {
            return denied;
        }

        var entity = new ProductType { IsActive = false, Version = 1 };
        Apply(entity, req);

        var duplicate = await HasActiveDuplicateAsync(entity.ProductCode, 0, ct);
        var evaluation = ProductDefinitionValidator.Validate(entity, duplicate);
        if (evaluation.IsHardStopped)
        {
            return EvaluationResult<ProductDefinitionDto>.Blocked(evaluation);
        }

        await _products.AddAsync(entity, ct);
        await UnitOfWork.SaveChangesAsync(ct);

        await ApplyAttributesAsync(entity.Id, req.Attributes, ct);
        await UnitOfWork.SaveChangesAsync(ct);

        var dto = await GetAsync(entity.Id, ct);
        RecordChange(EntityType, entity.Id, entity.Version, ConfigChangeAction.Create, AuditEventType.Configure,
            oldValue: null, newValue: dto, reason: req.ChangeReason);
        await UnitOfWork.SaveChangesAsync(ct);

        return EvaluationResult<ProductDefinitionDto>.Ok(dto!, evaluation);
    }

    public async Task<EvaluationResult<ProductDefinitionDto>> UpdateAsync(long id, SaveProductDefinitionRequest req, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(req);

        var denied = await RejectUnauthorizedEvalAsync(
            PermissionCodes.AdminProductsManage, ProductCatalogAuthorizationRule.EvaluateUpdate, ct);
        if (denied is not null)
        {
            return denied;
        }

        var entity = await _products.GetByIdAsync(id, ct);
        if (entity is null)
        {
            return EvaluationResult<ProductDefinitionDto>.Fail("Product not found.");
        }

        if (entity.IsActive && string.IsNullOrWhiteSpace(req.ChangeReason))
        {
            return EvaluationResult<ProductDefinitionDto>.Fail("A change reason is required to edit an active product.");
        }

        var before = await GetAsync(id, ct);
        Apply(entity, req);
        if (entity.IsActive)
        {
            entity.Version += 1;
        }

        var duplicate = await HasActiveDuplicateAsync(entity.ProductCode, entity.Id, ct);
        var evaluation = ProductDefinitionValidator.Validate(entity, duplicate);
        if (evaluation.IsHardStopped)
        {
            return EvaluationResult<ProductDefinitionDto>.Blocked(evaluation);
        }

        _products.Update(entity);
        await ApplyAttributesAsync(entity.Id, req.Attributes, ct);

        var after = await GetAsync(id, ct);
        RecordChange(EntityType, entity.Id, entity.Version, ConfigChangeAction.Update, AuditEventType.Configure,
            oldValue: before, newValue: after, reason: req.ChangeReason);
        await UnitOfWork.SaveChangesAsync(ct);

        return EvaluationResult<ProductDefinitionDto>.Ok(after!, evaluation);
    }

    public async Task<EvaluationResult<ProductDefinitionDto>> ActivateAsync(long id, string? reason, CancellationToken ct = default)
    {
        var denied = await RejectUnauthorizedEvalAsync(
            PermissionCodes.AdminConfigActivate, ProductCatalogAuthorizationRule.EvaluateActivate, ct);
        if (denied is not null)
        {
            return denied;
        }

        var entity = await _products.GetByIdAsync(id, ct);
        if (entity is null)
        {
            return EvaluationResult<ProductDefinitionDto>.Fail("Product not found.");
        }

        var duplicate = await HasActiveDuplicateAsync(entity.ProductCode, entity.Id, ct);
        var evaluation = ProductDefinitionValidator.Validate(entity, duplicate);
        if (evaluation.IsHardStopped)
        {
            return EvaluationResult<ProductDefinitionDto>.Blocked(evaluation);
        }

        entity.IsActive = true;
        _products.Update(entity);

        var dto = await GetAsync(id, ct);
        RecordChange(EntityType, entity.Id, entity.Version, ConfigChangeAction.Activate, AuditEventType.Activate,
            oldValue: null, newValue: dto, reason: reason);
        await UnitOfWork.SaveChangesAsync(ct);

        return EvaluationResult<ProductDefinitionDto>.Ok(dto!, evaluation);
    }

    public async Task<OperationResult<ProductDefinitionDto>> DeactivateAsync(long id, string? reason, CancellationToken ct = default)
    {
        var denied = await RejectUnauthorizedAsync(
            PermissionCodes.AdminConfigActivate, ProductCatalogAuthorizationRule.EvaluateDeactivate, ct);
        if (denied is not null)
        {
            return denied;
        }

        var entity = await _products.GetByIdAsync(id, ct);
        if (entity is null)
        {
            return OperationResult<ProductDefinitionDto>.Fail("Product not found.");
        }

        entity.IsActive = false;
        _products.Update(entity);

        var dto = await GetAsync(id, ct);
        RecordChange(EntityType, entity.Id, entity.Version, ConfigChangeAction.Deactivate, AuditEventType.Deactivate,
            oldValue: null, newValue: dto, reason: reason);
        await UnitOfWork.SaveChangesAsync(ct);

        return OperationResult<ProductDefinitionDto>.Ok(dto!);
    }

    private async Task ApplyAttributesAsync(long productId, IReadOnlyList<ProductAttributeSelection>? selections, CancellationToken ct)
    {
        selections ??= Array.Empty<ProductAttributeSelection>();
        var existing = await _assignments.ListAsync(a => a.ProductTypeId == productId, ct);
        var byAttr = existing.ToDictionary(a => a.ProductAttributeId);
        var requested = selections.ToDictionary(s => s.AttributeId);

        // Upsert requested assignments.
        foreach (var sel in selections)
        {
            if (byAttr.TryGetValue(sel.AttributeId, out var current))
            {
                current.IsActive = true;
                current.IsRequired = sel.IsRequired;
                _assignments.Update(current);
            }
            else
            {
                await _assignments.AddAsync(new ProductAttributeAssignment
                {
                    ProductTypeId = productId,
                    ProductAttributeId = sel.AttributeId,
                    IsRequired = sel.IsRequired,
                    IsActive = true
                }, ct);
            }
        }

        // Soft-remove assignments no longer requested (unique index forbids hard re-add).
        foreach (var current in existing.Where(a => a.IsActive && !requested.ContainsKey(a.ProductAttributeId)))
        {
            current.IsActive = false;
            _assignments.Update(current);
        }
    }

    private async Task<bool> HasActiveDuplicateAsync(string code, long selfId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return false;
        }

        var normalized = code.Trim();
        return await _products.AnyAsync(p => p.IsActive && p.Id != selfId && p.ProductCode == normalized, ct);
    }

    private static void Apply(ProductType e, SaveProductDefinitionRequest req)
    {
        e.ProductCode = (req.ProductCode ?? string.Empty).Trim();
        e.Name = req.Name?.Trim() ?? string.Empty;
        e.ComponentClass = req.ComponentClass;
        e.Category = req.Category?.Trim();
        e.DefaultShelfLifeHours = req.DefaultShelfLifeHours;
        e.RequiresCrossmatch = req.RequiresCrossmatch;
        e.RequiresAboMatch = req.RequiresAboMatch;
        e.RequiresRhMatch = req.RequiresRhMatch;
        e.RequiresRetype = req.RequiresRetype;
        e.Isbt128ProductCode = req.Isbt128ProductCode?.Trim();
        e.DefaultChargeCode = req.DefaultChargeCode?.Trim();
        e.StorageRequirements = req.StorageRequirements?.Trim();
        e.IssueRules = req.IssueRules?.Trim();
        e.ReturnRules = req.ReturnRules?.Trim();
        e.ModificationRules = req.ModificationRules?.Trim();
    }

    private static ProductDefinitionDto Map(
        ProductType p,
        IReadOnlyList<ProductAttributeAssignment> assignments,
        IReadOnlyList<ProductAttribute> attributes)
    {
        var attrById = attributes.ToDictionary(a => a.Id);
        var assigned = assignments
            .Where(a => a.ProductTypeId == p.Id && a.IsActive && attrById.ContainsKey(a.ProductAttributeId))
            .Select(a =>
            {
                var attr = attrById[a.ProductAttributeId];
                return new ProductAttributeAssignmentDto(attr.Id, attr.Code, attr.Name, a.IsRequired);
            })
            .OrderBy(a => a.Code)
            .ToList();

        return new ProductDefinitionDto(
            p.Id, p.ProductCode, p.Name, p.ComponentClass, p.Category, p.DefaultShelfLifeHours,
            p.RequiresCrossmatch, p.RequiresAboMatch, p.RequiresRhMatch, p.RequiresRetype, p.Isbt128ProductCode, p.DefaultChargeCode,
            p.StorageRequirements, p.IssueRules, p.ReturnRules, p.ModificationRules, p.Version, p.IsActive, assigned);
    }

    private async Task<EvaluationResult<ProductDefinitionDto>?> RejectUnauthorizedEvalAsync(
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
            ? EvaluationResult<ProductDefinitionDto>.Blocked(new RuleEvaluation([auth]))
            : null;
    }

    private async Task<OperationResult<ProductDefinitionDto>?> RejectUnauthorizedAsync(
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
            ? OperationResult<ProductDefinitionDto>.Fail(auth.Message)
            : null;
    }
}
