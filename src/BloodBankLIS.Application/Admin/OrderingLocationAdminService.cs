using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Common;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Application.Admin;

public sealed class OrderingLocationAdminService : ConfigAdminServiceBase
{
    private readonly IRepository<OrderingLocation> _locations;

    public OrderingLocationAdminService(
        IRepository<OrderingLocation> locations,
        IUnitOfWork unitOfWork,
        IClock clock,
        ICurrentUser currentUser,
        IAuditWriter audit,
        IConfigurationHistoryWriter history)
        : base(unitOfWork, clock, currentUser, audit, history)
    {
        _locations = locations;
    }

    public async Task<IReadOnlyList<OrderingLocationDto>> ListAsync(bool includeInactive, CancellationToken ct = default)
    {
        var list = includeInactive
            ? await _locations.ListAsync(ct)
            : await _locations.ListAsync(l => l.IsActive, ct);
        return list.OrderBy(l => l.Code).Select(OrderingLocationDto.From).ToList();
    }

    public async Task<OrderingLocationDto?> GetAsync(long id, CancellationToken ct = default)
    {
        var entity = await _locations.GetByIdAsync(id, ct);
        return entity is null ? null : OrderingLocationDto.From(entity);
    }

    public async Task<EvaluationResult<OrderingLocationDto>> CreateAsync(SaveOrderingLocationRequest request, CancellationToken ct = default)
    {
        var code = NormalizeCode(request.Code);
        if (await _locations.AnyAsync(l => l.Code == code, ct))
        {
            return EvaluationResult<OrderingLocationDto>.Blocked(new RuleEvaluation([
                RuleResult.HardStop("LOCATION.CODE.DUPLICATE", $"Location code '{code}' already exists.")]));
        }

        var entity = BuildEntity(code, request);
        var validation = OrderingLocationValidator.Validate(entity, duplicateCode: false);
        if (validation.IsHardStopped)
        {
            return EvaluationResult<OrderingLocationDto>.Blocked(validation);
        }

        await _locations.AddAsync(entity, ct);
        await UnitOfWork.SaveChangesAsync(ct);

        var dto = OrderingLocationDto.From(entity);
        RecordChange("OrderingLocation", entity.Id, 1, ConfigChangeAction.Create, AuditEventType.Create, null, dto, null);
        await UnitOfWork.SaveChangesAsync(ct);
        return EvaluationResult<OrderingLocationDto>.Ok(dto, validation);
    }

    public async Task<EvaluationResult<OrderingLocationDto>> UpdateAsync(long id, SaveOrderingLocationRequest request, CancellationToken ct = default)
    {
        var entity = await _locations.GetByIdAsync(id, ct);
        if (entity is null)
        {
            return EvaluationResult<OrderingLocationDto>.Fail("Location not found.");
        }

        var code = NormalizeCode(request.Code);
        var duplicate = await _locations.AnyAsync(l => l.Code == code && l.Id != id, ct);
        var old = OrderingLocationDto.From(entity);

        Apply(entity, code, request);

        var validation = OrderingLocationValidator.Validate(entity, duplicate);
        if (validation.IsHardStopped)
        {
            return EvaluationResult<OrderingLocationDto>.Blocked(validation);
        }

        _locations.Update(entity);
        var dto = OrderingLocationDto.From(entity);
        RecordChange("OrderingLocation", entity.Id, 1, ConfigChangeAction.Update, AuditEventType.Update, old, dto, null);
        await UnitOfWork.SaveChangesAsync(ct);
        return EvaluationResult<OrderingLocationDto>.Ok(dto, validation);
    }

    public async Task<EvaluationResult<OrderingLocationDto>> SetActiveAsync(long id, bool active, CancellationToken ct = default)
    {
        var entity = await _locations.GetByIdAsync(id, ct);
        if (entity is null)
        {
            return EvaluationResult<OrderingLocationDto>.Fail("Location not found.");
        }

        var old = OrderingLocationDto.From(entity);
        entity.IsActive = active;
        _locations.Update(entity);
        var dto = OrderingLocationDto.From(entity);
        var action = active ? ConfigChangeAction.Activate : ConfigChangeAction.Deactivate;
        RecordChange("OrderingLocation", entity.Id, 1, action, ToAuditType(action), old, dto, null);
        await UnitOfWork.SaveChangesAsync(ct);
        return EvaluationResult<OrderingLocationDto>.Ok(dto, new RuleEvaluation([]));
    }

    private static string NormalizeCode(string code) => code.Trim().ToUpperInvariant();

    private static OrderingLocation BuildEntity(string code, SaveOrderingLocationRequest request)
    {
        var entity = new OrderingLocation { Code = code, IsActive = true };
        Apply(entity, code, request);
        return entity;
    }

    private static void Apply(OrderingLocation entity, string code, SaveOrderingLocationRequest request)
    {
        entity.Code = code;
        entity.Name = string.IsNullOrWhiteSpace(request.Name) ? code : request.Name.Trim();
        entity.Department = NullIfEmpty(request.Department);
        entity.Hl7MappingCode = string.IsNullOrWhiteSpace(request.Hl7MappingCode)
            ? code
            : request.Hl7MappingCode.Trim().ToUpperInvariant();
    }

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
