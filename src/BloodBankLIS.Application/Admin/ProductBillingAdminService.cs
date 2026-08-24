using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Common;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Application.Admin;

public sealed class ProductBillingAdminService : ConfigAdminServiceBase
{
    private const string EntityType = nameof(ProductBilling);

    private readonly IRepository<ProductBilling> _rows;

    public ProductBillingAdminService(
        IRepository<ProductBilling> rows,
        IUnitOfWork unitOfWork,
        IClock clock,
        ICurrentUser currentUser,
        IAuditWriter audit,
        IConfigurationHistoryWriter history)
        : base(unitOfWork, clock, currentUser, audit, history)
    {
        _rows = rows;
    }

    public async Task<IReadOnlyList<ProductBillingDto>> ListAsync(bool includeInactive, CancellationToken ct = default)
    {
        var list = includeInactive
            ? await _rows.ListAsync(ct)
            : await _rows.ListAsync(r => r.IsActive, ct);
        return list.OrderBy(r => r.IsbtProductCode).ThenBy(r => r.BillingCode).Select(ProductBillingDto.From).ToList();
    }

    public async Task<ProductBillingDto?> GetAsync(long id, CancellationToken ct = default)
    {
        var entity = await _rows.GetByIdAsync(id, ct);
        return entity is null ? null : ProductBillingDto.From(entity);
    }

    public async Task<EvaluationResult<ProductBillingDto>> CreateAsync(SaveProductBillingRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var entity = new ProductBilling { IsActive = true };
        Apply(entity, request);

        var duplicate = await IsDuplicateAsync(entity, excludeId: null, ct);
        var validation = ProductBillingValidator.Validate(entity, duplicate);
        if (validation.IsHardStopped)
        {
            return EvaluationResult<ProductBillingDto>.Blocked(validation);
        }

        await _rows.AddAsync(entity, ct);
        await UnitOfWork.SaveChangesAsync(ct);

        var dto = ProductBillingDto.From(entity);
        RecordChange(EntityType, entity.Id, 1, ConfigChangeAction.Create, AuditEventType.Create, null, dto, null);
        await UnitOfWork.SaveChangesAsync(ct);
        return EvaluationResult<ProductBillingDto>.Ok(dto, validation);
    }

    public async Task<EvaluationResult<ProductBillingDto>> UpdateAsync(long id, SaveProductBillingRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var entity = await _rows.GetByIdAsync(id, ct);
        if (entity is null)
        {
            return EvaluationResult<ProductBillingDto>.Fail("Product billing row not found.");
        }

        var old = ProductBillingDto.From(entity);
        Apply(entity, request);

        var duplicate = await IsDuplicateAsync(entity, id, ct);
        var validation = ProductBillingValidator.Validate(entity, duplicate);
        if (validation.IsHardStopped)
        {
            return EvaluationResult<ProductBillingDto>.Blocked(validation);
        }

        _rows.Update(entity);
        var dto = ProductBillingDto.From(entity);
        RecordChange(EntityType, entity.Id, 1, ConfigChangeAction.Update, AuditEventType.Update, old, dto, null);
        await UnitOfWork.SaveChangesAsync(ct);
        return EvaluationResult<ProductBillingDto>.Ok(dto, validation);
    }

    public async Task<EvaluationResult<ProductBillingDto>> SetActiveAsync(long id, bool active, CancellationToken ct = default)
    {
        var entity = await _rows.GetByIdAsync(id, ct);
        if (entity is null)
        {
            return EvaluationResult<ProductBillingDto>.Fail("Product billing row not found.");
        }

        if (active)
        {
            var duplicate = await IsDuplicateAsync(entity, id, ct);
            var validation = ProductBillingValidator.Validate(entity, duplicate);
            if (validation.IsHardStopped)
            {
                return EvaluationResult<ProductBillingDto>.Blocked(validation);
            }
        }

        var old = ProductBillingDto.From(entity);
        entity.IsActive = active;
        _rows.Update(entity);
        var dto = ProductBillingDto.From(entity);
        var action = active ? ConfigChangeAction.Activate : ConfigChangeAction.Deactivate;
        RecordChange(EntityType, entity.Id, 1, action, ToAuditType(action), old, dto, null);
        await UnitOfWork.SaveChangesAsync(ct);
        return EvaluationResult<ProductBillingDto>.Ok(dto, new RuleEvaluation([]));
    }

    private async Task<bool> IsDuplicateAsync(ProductBilling entity, long? excludeId, CancellationToken ct) =>
        await _rows.AnyAsync(r =>
            r.IsActive
            && r.Trigger == entity.Trigger
            && r.IsbtProductCode == entity.IsbtProductCode
            && r.BillingCode == entity.BillingCode
            && (excludeId == null || r.Id != excludeId), ct);

    private static void Apply(ProductBilling entity, SaveProductBillingRequest request)
    {
        entity.BillingCode = (request.BillingCode ?? string.Empty).Trim().ToUpperInvariant();
        entity.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        entity.Price = request.Price;
        entity.Trigger = request.Trigger;
        entity.IsbtProductCode = (request.IsbtProductCode ?? string.Empty).Trim().ToUpperInvariant();
    }
}
