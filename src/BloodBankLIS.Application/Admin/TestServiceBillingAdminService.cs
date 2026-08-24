using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Common;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Application.Admin;

public sealed class TestServiceBillingAdminService : ConfigAdminServiceBase
{
    private const string EntityType = nameof(TestServiceBilling);

    private readonly IRepository<TestServiceBilling> _rows;

    public TestServiceBillingAdminService(
        IRepository<TestServiceBilling> rows,
        IUnitOfWork unitOfWork,
        IClock clock,
        ICurrentUser currentUser,
        IAuditWriter audit,
        IConfigurationHistoryWriter history)
        : base(unitOfWork, clock, currentUser, audit, history)
    {
        _rows = rows;
    }

    public async Task<IReadOnlyList<TestServiceBillingDto>> ListAsync(bool includeInactive, CancellationToken ct = default)
    {
        var list = includeInactive
            ? await _rows.ListAsync(ct)
            : await _rows.ListAsync(r => r.IsActive, ct);
        return list.OrderBy(r => r.TestCode).ThenBy(r => r.BillingCode).Select(TestServiceBillingDto.From).ToList();
    }

    public async Task<TestServiceBillingDto?> GetAsync(long id, CancellationToken ct = default)
    {
        var entity = await _rows.GetByIdAsync(id, ct);
        return entity is null ? null : TestServiceBillingDto.From(entity);
    }

    public async Task<EvaluationResult<TestServiceBillingDto>> CreateAsync(SaveTestServiceBillingRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var entity = new TestServiceBilling { IsActive = true };
        Apply(entity, request);

        var duplicate = await IsDuplicateAsync(entity, excludeId: null, ct);
        var validation = TestServiceBillingValidator.Validate(entity, duplicate);
        if (validation.IsHardStopped)
        {
            return EvaluationResult<TestServiceBillingDto>.Blocked(validation);
        }

        await _rows.AddAsync(entity, ct);
        await UnitOfWork.SaveChangesAsync(ct);

        var dto = TestServiceBillingDto.From(entity);
        RecordChange(EntityType, entity.Id, 1, ConfigChangeAction.Create, AuditEventType.Create, null, dto, null);
        await UnitOfWork.SaveChangesAsync(ct);
        return EvaluationResult<TestServiceBillingDto>.Ok(dto, validation);
    }

    public async Task<EvaluationResult<TestServiceBillingDto>> UpdateAsync(long id, SaveTestServiceBillingRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var entity = await _rows.GetByIdAsync(id, ct);
        if (entity is null)
        {
            return EvaluationResult<TestServiceBillingDto>.Fail("Test/service billing row not found.");
        }

        var old = TestServiceBillingDto.From(entity);
        Apply(entity, request);

        var duplicate = await IsDuplicateAsync(entity, id, ct);
        var validation = TestServiceBillingValidator.Validate(entity, duplicate);
        if (validation.IsHardStopped)
        {
            return EvaluationResult<TestServiceBillingDto>.Blocked(validation);
        }

        _rows.Update(entity);
        var dto = TestServiceBillingDto.From(entity);
        RecordChange(EntityType, entity.Id, 1, ConfigChangeAction.Update, AuditEventType.Update, old, dto, null);
        await UnitOfWork.SaveChangesAsync(ct);
        return EvaluationResult<TestServiceBillingDto>.Ok(dto, validation);
    }

    public async Task<EvaluationResult<TestServiceBillingDto>> SetActiveAsync(long id, bool active, CancellationToken ct = default)
    {
        var entity = await _rows.GetByIdAsync(id, ct);
        if (entity is null)
        {
            return EvaluationResult<TestServiceBillingDto>.Fail("Test/service billing row not found.");
        }

        if (active)
        {
            var duplicate = await IsDuplicateAsync(entity, id, ct);
            var validation = TestServiceBillingValidator.Validate(entity, duplicate);
            if (validation.IsHardStopped)
            {
                return EvaluationResult<TestServiceBillingDto>.Blocked(validation);
            }
        }

        var old = TestServiceBillingDto.From(entity);
        entity.IsActive = active;
        _rows.Update(entity);
        var dto = TestServiceBillingDto.From(entity);
        var action = active ? ConfigChangeAction.Activate : ConfigChangeAction.Deactivate;
        RecordChange(EntityType, entity.Id, 1, action, ToAuditType(action), old, dto, null);
        await UnitOfWork.SaveChangesAsync(ct);
        return EvaluationResult<TestServiceBillingDto>.Ok(dto, new RuleEvaluation([]));
    }

    private async Task<bool> IsDuplicateAsync(TestServiceBilling entity, long? excludeId, CancellationToken ct) =>
        await _rows.AnyAsync(r =>
            r.IsActive
            && r.Trigger == entity.Trigger
            && r.TestCode == entity.TestCode
            && r.BillingCode == entity.BillingCode
            && (excludeId == null || r.Id != excludeId), ct);

    private static void Apply(TestServiceBilling entity, SaveTestServiceBillingRequest request)
    {
        entity.BillingCode = (request.BillingCode ?? string.Empty).Trim().ToUpperInvariant();
        entity.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        entity.Price = request.Price;
        entity.Trigger = request.Trigger;
        entity.TestCode = (request.TestCode ?? string.Empty).Trim().ToUpperInvariant();
    }
}
