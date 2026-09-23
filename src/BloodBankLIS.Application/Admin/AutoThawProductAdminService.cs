using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Common;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Application.Admin;

/// <summary>
/// Admin list of facility product codes that can be thawed in place at allocation.
/// </summary>
public sealed class AutoThawProductAdminService : ConfigAdminServiceBase
{
    private const string EntityType = nameof(AutoThawProduct);

    private readonly IRepository<AutoThawProduct> _rows;
    private readonly IRepository<ProductType> _productTypes;
    private readonly IPermissionEvaluator? _permissionEvaluator;

    public AutoThawProductAdminService(
        IRepository<AutoThawProduct> rows,
        IRepository<ProductType> productTypes,
        IUnitOfWork unitOfWork,
        IClock clock,
        ICurrentUser currentUser,
        IAuditWriter audit,
        IConfigurationHistoryWriter history,
        IPermissionEvaluator? permissionEvaluator = null)
        : base(unitOfWork, clock, currentUser, audit, history)
    {
        _rows = rows;
        _productTypes = productTypes;
        _permissionEvaluator = permissionEvaluator;
    }

    public async Task<IReadOnlyList<AutoThawProductDto>> ListAsync(bool includeInactive, CancellationToken ct = default)
    {
        var rows = includeInactive
            ? await _rows.ListAsync(ct)
            : await _rows.ListAsync(r => r.IsActive, ct);
        var products = await LoadProductMapAsync(ct);
        return rows
            .OrderBy(r => products.GetValueOrDefault(r.ProductTypeId)?.ProductCode ?? string.Empty)
            .Select(r => AutoThawProductDto.From(r, products.GetValueOrDefault(r.ProductTypeId)))
            .ToList();
    }

    public async Task<AutoThawProductDto?> GetAsync(long id, CancellationToken ct = default)
    {
        var entity = await _rows.GetByIdAsync(id, ct);
        if (entity is null)
        {
            return null;
        }

        var product = await _productTypes.GetByIdAsync(entity.ProductTypeId, ct);
        return AutoThawProductDto.From(entity, product);
    }

    public async Task<EvaluationResult<AutoThawProductDto>> CreateAsync(
        SaveAutoThawProductRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var denied = await RejectUnauthorizedAsync(
            PermissionCodes.AdminConfigEdit, AutoThawProductAuthorizationRule.EvaluateCreate, ct);
        if (denied is not null)
        {
            return denied;
        }

        var product = await _productTypes.GetByIdAsync(request.ProductTypeId, ct);
        var entity = new AutoThawProduct
        {
            ProductTypeId = request.ProductTypeId,
            IsActive = true
        };
        var duplicate = await _rows.AnyAsync(r => r.ProductTypeId == request.ProductTypeId, ct);
        var validation = AutoThawProductValidator.Validate(
            entity,
            productTypeMissing: product is null || !product.IsActive,
            duplicateProductType: duplicate);
        if (validation.IsHardStopped)
        {
            return EvaluationResult<AutoThawProductDto>.Blocked(validation);
        }

        await _rows.AddAsync(entity, ct);
        await UnitOfWork.SaveChangesAsync(ct);

        var dto = AutoThawProductDto.From(entity, product);
        RecordChange(EntityType, entity.Id, 1, ConfigChangeAction.Create, AuditEventType.Configure, null, dto, null);
        await UnitOfWork.SaveChangesAsync(ct);
        return EvaluationResult<AutoThawProductDto>.Ok(dto, validation);
    }

    public async Task<EvaluationResult<AutoThawProductDto>> SetActiveAsync(
        long id, bool active, CancellationToken ct = default)
    {
        var denied = await RejectUnauthorizedAsync(
            PermissionCodes.AdminConfigActivate,
            active ? AutoThawProductAuthorizationRule.EvaluateActivate : AutoThawProductAuthorizationRule.EvaluateDeactivate,
            ct);
        if (denied is not null)
        {
            return denied;
        }

        var entity = await _rows.GetByIdAsync(id, ct);
        if (entity is null)
        {
            return EvaluationResult<AutoThawProductDto>.Fail("Auto-thaw product not found.");
        }

        var product = await _productTypes.GetByIdAsync(entity.ProductTypeId, ct);
        if (active)
        {
            var validation = AutoThawProductValidator.Validate(entity, product is null || !product.IsActive, duplicateProductType: false);
            if (validation.IsHardStopped)
            {
                return EvaluationResult<AutoThawProductDto>.Blocked(validation);
            }
        }

        var old = AutoThawProductDto.From(entity, product);
        entity.IsActive = active;
        _rows.Update(entity);
        var dto = AutoThawProductDto.From(entity, product);
        var action = active ? ConfigChangeAction.Activate : ConfigChangeAction.Deactivate;
        RecordChange(EntityType, entity.Id, 1, action, ToAuditType(action), old, dto, null);
        await UnitOfWork.SaveChangesAsync(ct);
        return EvaluationResult<AutoThawProductDto>.Ok(dto, new RuleEvaluation([]));
    }

    private async Task<Dictionary<long, ProductType>> LoadProductMapAsync(CancellationToken ct)
    {
        var products = await _productTypes.ListAsync(ct);
        return products.ToDictionary(p => p.Id);
    }

    private async Task<EvaluationResult<AutoThawProductDto>?> RejectUnauthorizedAsync(
        string permissionCode,
        Func<bool, RuleResult> evaluate,
        CancellationToken ct)
    {
        if (_permissionEvaluator is null)
        {
            return null;
        }

        var allowed = await _permissionEvaluator.HasPermissionAsync(CurrentUser.UserName, permissionCode, ct);
        var auth = evaluate(allowed);
        return auth.Severity == RuleSeverity.HardStop
            ? EvaluationResult<AutoThawProductDto>.Blocked(new RuleEvaluation([auth]))
            : null;
    }
}
