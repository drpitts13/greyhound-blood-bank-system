using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Common;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Domain.Rules.Config;

namespace BloodBankLIS.Application.Admin;

public sealed class BloodAttributeAdminService : ConfigAdminServiceBase
{
    private const string EntityType = nameof(BloodAttributeDefinition);

    private readonly IRepository<BloodAttributeDefinition> _repo;

    public BloodAttributeAdminService(
        IRepository<BloodAttributeDefinition> repo,
        IUnitOfWork unitOfWork,
        IClock clock,
        ICurrentUser currentUser,
        IAuditWriter audit,
        IConfigurationHistoryWriter history)
        : base(unitOfWork, clock, currentUser, audit, history)
    {
        _repo = repo;
    }

    public async Task<IReadOnlyList<BloodAttributeDefinitionDto>> ListAsync(bool includeInactive, CancellationToken ct = default)
    {
        var items = includeInactive
            ? await _repo.ListAsync(ct)
            : await _repo.ListAsync(d => d.IsActive, ct);
        return items.OrderBy(d => d.SortOrder).ThenBy(d => d.Code).Select(BloodAttributeDefinitionDtoMapping.From).ToList();
    }

    public async Task<BloodAttributeDefinitionDto?> GetAsync(long id, CancellationToken ct = default)
    {
        var item = await _repo.GetByIdAsync(id, ct);
        return item is null ? null : BloodAttributeDefinitionDtoMapping.From(item);
    }

    public async Task<EvaluationResult<BloodAttributeDefinitionDto>> CreateAsync(SaveBloodAttributeDefinitionRequest req, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(req);

        var entity = new BloodAttributeDefinition { IsDraft = true, IsActive = false, Version = 1 };
        Apply(entity, req);

        var duplicate = await HasActiveDuplicateAsync(entity.Code, 0, ct);
        var evaluation = BloodAttributeDefinitionValidator.Validate(entity, duplicate);
        if (evaluation.IsHardStopped)
        {
            return EvaluationResult<BloodAttributeDefinitionDto>.Blocked(evaluation);
        }

        await _repo.AddAsync(entity, ct);
        await UnitOfWork.SaveChangesAsync(ct);

        RecordChange(EntityType, entity.Id, entity.Version, ConfigChangeAction.Create, AuditEventType.Create,
            oldValue: null, newValue: BloodAttributeDefinitionDtoMapping.From(entity), reason: req.ChangeReason);
        await UnitOfWork.SaveChangesAsync(ct);

        return EvaluationResult<BloodAttributeDefinitionDto>.Ok(BloodAttributeDefinitionDtoMapping.From(entity), evaluation);
    }

    public async Task<EvaluationResult<BloodAttributeDefinitionDto>> UpdateAsync(long id, SaveBloodAttributeDefinitionRequest req, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(req);

        var entity = await _repo.GetByIdAsync(id, ct);
        if (entity is null)
        {
            return EvaluationResult<BloodAttributeDefinitionDto>.Fail("Blood attribute definition not found.");
        }

        if (entity.IsActive && string.IsNullOrWhiteSpace(req.ChangeReason))
        {
            return EvaluationResult<BloodAttributeDefinitionDto>.Fail("A change reason is required to edit an active blood attribute definition.");
        }

        var before = BloodAttributeDefinitionDtoMapping.From(entity);
        Apply(entity, req);
        if (entity.IsActive)
        {
            entity.Version += 1;
        }

        var duplicate = await HasActiveDuplicateAsync(entity.Code, entity.Id, ct);
        var evaluation = BloodAttributeDefinitionValidator.Validate(entity, duplicate);
        if (evaluation.IsHardStopped)
        {
            return EvaluationResult<BloodAttributeDefinitionDto>.Blocked(evaluation);
        }

        _repo.Update(entity);
        RecordChange(EntityType, entity.Id, entity.Version, ConfigChangeAction.Update, AuditEventType.Update,
            oldValue: before, newValue: BloodAttributeDefinitionDtoMapping.From(entity), reason: req.ChangeReason);
        await UnitOfWork.SaveChangesAsync(ct);

        return EvaluationResult<BloodAttributeDefinitionDto>.Ok(BloodAttributeDefinitionDtoMapping.From(entity), evaluation);
    }

    public async Task<EvaluationResult<BloodAttributeDefinitionDto>> ActivateAsync(long id, string? reason, CancellationToken ct = default)
    {
        var entity = await _repo.GetByIdAsync(id, ct);
        if (entity is null)
        {
            return EvaluationResult<BloodAttributeDefinitionDto>.Fail("Blood attribute definition not found.");
        }

        var duplicate = await HasActiveDuplicateAsync(entity.Code, entity.Id, ct);
        var evaluation = BloodAttributeDefinitionValidator.Validate(entity, duplicate);
        if (evaluation.IsHardStopped)
        {
            return EvaluationResult<BloodAttributeDefinitionDto>.Blocked(evaluation);
        }

        entity.IsActive = true;
        entity.IsDraft = false;
        entity.RetiredUtc = null;
        entity.EffectiveUtc ??= Clock.UtcNow;
        entity.ChangeReason = reason;
        _repo.Update(entity);

        RecordChange(EntityType, entity.Id, entity.Version, ConfigChangeAction.Activate, AuditEventType.Activate,
            oldValue: null, newValue: BloodAttributeDefinitionDtoMapping.From(entity), reason: reason);
        await UnitOfWork.SaveChangesAsync(ct);

        return EvaluationResult<BloodAttributeDefinitionDto>.Ok(BloodAttributeDefinitionDtoMapping.From(entity), evaluation);
    }

    public async Task<OperationResult<BloodAttributeDefinitionDto>> DeactivateAsync(long id, string? reason, CancellationToken ct = default)
    {
        var entity = await _repo.GetByIdAsync(id, ct);
        if (entity is null)
        {
            return OperationResult<BloodAttributeDefinitionDto>.Fail("Blood attribute definition not found.");
        }

        entity.IsActive = false;
        entity.RetiredUtc = Clock.UtcNow;
        entity.ChangeReason = reason;
        _repo.Update(entity);

        RecordChange(EntityType, entity.Id, entity.Version, ConfigChangeAction.Deactivate, AuditEventType.Deactivate,
            oldValue: null, newValue: BloodAttributeDefinitionDtoMapping.From(entity), reason: reason);
        await UnitOfWork.SaveChangesAsync(ct);

        return OperationResult<BloodAttributeDefinitionDto>.Ok(BloodAttributeDefinitionDtoMapping.From(entity));
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

    private static void Apply(BloodAttributeDefinition e, SaveBloodAttributeDefinitionRequest req)
    {
        e.Code = (req.Code ?? string.Empty).Trim();
        e.Name = req.Name?.Trim() ?? string.Empty;
        e.AntibodyName = req.AntibodyName?.Trim() ?? string.Empty;
        e.IsClinicallySignificant = req.IsClinicallySignificant;
        e.SortOrder = req.SortOrder;
        e.ChangeReason = req.ChangeReason;
    }
}
