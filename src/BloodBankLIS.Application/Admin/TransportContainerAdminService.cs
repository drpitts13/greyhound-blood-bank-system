using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Common;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Application.Admin;

public sealed record TransportContainerDto(
    long Id,
    string Code,
    string Name,
    TransportContainerType ContainerType,
    string? Description,
    decimal? MinTemperatureC,
    decimal? MaxTemperatureC,
    int? ValidatedHours,
    int SortOrder,
    bool IsActive)
{
    public static TransportContainerDto From(TransportContainer c) => new(
        c.Id, c.Code, c.Name, c.ContainerType, c.Description, c.MinTemperatureC, c.MaxTemperatureC, c.ValidatedHours, c.SortOrder, c.IsActive);

    /// <summary>For example "1.0 to 10.0 °C", "at most 10.0 °C", or "No range set".</summary>
    public string RangeText => (MinTemperatureC, MaxTemperatureC) switch
    {
        (decimal min, decimal max) => $"{min:0.0} to {max:0.0} °C",
        (decimal min, null) => $"at least {min:0.0} °C",
        (null, decimal max) => $"at most {max:0.0} °C",
        _ => "No range set"
    };
}

public sealed record SaveTransportContainerRequest(
    string Code,
    string Name,
    TransportContainerType ContainerType,
    string? Description,
    decimal? MinTemperatureC,
    decimal? MaxTemperatureC,
    int? ValidatedHours,
    int SortOrder,
    // Why an existing entry is changed; required for an update.
    string? Reason = null);

/// <summary>
/// Facility catalog of coolers and other transport vessels. Nothing is seeded; ranges and validated hours are the
/// facility's own validation results. Versioned through configuration history.
/// </summary>
public sealed class TransportContainerAdminService : ConfigAdminServiceBase
{
    private const string EntityType = nameof(TransportContainer);

    private readonly IRepository<TransportContainer> _containers;

    public TransportContainerAdminService(
        IRepository<TransportContainer> containers,
        IUnitOfWork unitOfWork,
        IClock clock,
        ICurrentUser currentUser,
        IAuditWriter audit,
        IConfigurationHistoryWriter history)
        : base(unitOfWork, clock, currentUser, audit, history)
    {
        _containers = containers;
    }

    public async Task<IReadOnlyList<TransportContainerDto>> ListAsync(CancellationToken ct = default) =>
        (await _containers.ListAsync(ct))
            .OrderBy(c => c.SortOrder).ThenBy(c => c.Code)
            .Select(TransportContainerDto.From)
            .ToList();

    public async Task<IReadOnlyList<TransportContainerDto>> ListActiveAsync(CancellationToken ct = default) =>
        (await ListAsync(ct)).Where(c => c.IsActive).ToList();

    public async Task<EvaluationResult<TransportContainerDto>> CreateAsync(SaveTransportContainerRequest request, CancellationToken ct = default)
    {
        var entity = new TransportContainer { IsActive = true };
        Apply(entity, request);
        var validation = TransportContainerRule.Validate(entity, await _containers.AnyAsync(c => c.Code == entity.Code, ct));
        if (validation.IsHardStopped)
        {
            return EvaluationResult<TransportContainerDto>.Blocked(validation);
        }

        await _containers.AddAsync(entity, ct);
        await UnitOfWork.SaveChangesAsync(ct);
        var dto = TransportContainerDto.From(entity);
        RecordChange(EntityType, entity.Id, 1, ConfigChangeAction.Create, AuditEventType.Configure, null, dto, null);
        await UnitOfWork.SaveChangesAsync(ct);
        return EvaluationResult<TransportContainerDto>.Ok(dto);
    }

    public async Task<EvaluationResult<TransportContainerDto>> UpdateAsync(long id, SaveTransportContainerRequest request, CancellationToken ct = default)
    {
        var why = ValidatedConfigChangeReasonRule.Evaluate(request.Reason, "a transport container");
        if (why.Severity == RuleSeverity.HardStop)
        {
            return EvaluationResult<TransportContainerDto>.Blocked(new RuleEvaluation([why]));
        }

        var entity = await _containers.GetByIdAsync(id, ct);
        if (entity is null)
        {
            return EvaluationResult<TransportContainerDto>.Fail("Container not found.");
        }

        var old = TransportContainerDto.From(entity);
        Apply(entity, request);
        var validation = TransportContainerRule.Validate(entity, await _containers.AnyAsync(c => c.Id != id && c.Code == entity.Code, ct));
        if (validation.IsHardStopped)
        {
            return EvaluationResult<TransportContainerDto>.Blocked(validation);
        }

        _containers.Update(entity);
        var dto = TransportContainerDto.From(entity);
        RecordChange(EntityType, entity.Id, 1, ConfigChangeAction.Update, AuditEventType.Configure, old, dto, request.Reason!.Trim());
        await UnitOfWork.SaveChangesAsync(ct);
        return EvaluationResult<TransportContainerDto>.Ok(dto);
    }

    public async Task<EvaluationResult<TransportContainerDto>> SetActiveAsync(long id, bool active, string? reason = null, CancellationToken ct = default)
    {
        var why = ValidatedConfigChangeReasonRule.Evaluate(reason, "a transport container");
        if (why.Severity == RuleSeverity.HardStop)
        {
            return EvaluationResult<TransportContainerDto>.Blocked(new RuleEvaluation([why]));
        }

        var entity = await _containers.GetByIdAsync(id, ct);
        if (entity is null)
        {
            return EvaluationResult<TransportContainerDto>.Fail("Container not found.");
        }

        var old = TransportContainerDto.From(entity);
        entity.IsActive = active;
        _containers.Update(entity);
        var dto = TransportContainerDto.From(entity);
        RecordChange(EntityType, entity.Id, 1,
            active ? ConfigChangeAction.Activate : ConfigChangeAction.Deactivate,
            active ? AuditEventType.Activate : AuditEventType.Deactivate, old, dto, reason!.Trim());
        await UnitOfWork.SaveChangesAsync(ct);
        return EvaluationResult<TransportContainerDto>.Ok(dto);
    }

    private static void Apply(TransportContainer entity, SaveTransportContainerRequest r)
    {
        entity.Code = (r.Code ?? string.Empty).Trim().ToUpperInvariant();
        entity.Name = (r.Name ?? string.Empty).Trim();
        entity.ContainerType = r.ContainerType;
        entity.Description = string.IsNullOrWhiteSpace(r.Description) ? null : r.Description.Trim();
        entity.MinTemperatureC = r.MinTemperatureC;
        entity.MaxTemperatureC = r.MaxTemperatureC;
        entity.ValidatedHours = r.ValidatedHours;
        entity.SortOrder = r.SortOrder;
    }
}
