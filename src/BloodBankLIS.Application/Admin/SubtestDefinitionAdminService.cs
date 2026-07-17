using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Common;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Domain.Rules.Config;
using BloodBankLIS.Domain.ValueObjects;

namespace BloodBankLIS.Application.Admin;

public sealed class SubtestDefinitionAdminService : ConfigAdminServiceBase
{
    private const string EntityType = nameof(SubtestDefinition);

    private readonly IRepository<SubtestDefinition> _repo;

    public SubtestDefinitionAdminService(
        IRepository<SubtestDefinition> repo,
        IUnitOfWork unitOfWork,
        IClock clock,
        ICurrentUser currentUser,
        IAuditWriter audit,
        IConfigurationHistoryWriter history)
        : base(unitOfWork, clock, currentUser, audit, history)
    {
        _repo = repo;
    }

    public async Task<IReadOnlyList<SubtestDefinitionDto>> ListAsync(bool includeInactive, CancellationToken ct = default)
    {
        var items = includeInactive
            ? await _repo.ListAsync(ct)
            : await _repo.ListAsync(s => s.IsActive, ct);
        return items.OrderBy(s => s.Code).Select(Map).ToList();
    }

    public async Task<SubtestDefinitionDto?> GetAsync(long id, CancellationToken ct = default)
    {
        var item = await _repo.GetByIdAsync(id, ct);
        return item is null ? null : Map(item);
    }

    public async Task<EvaluationResult<SubtestDefinitionDto>> CreateAsync(SaveSubtestDefinitionRequest req, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(req);

        var entity = new SubtestDefinition { IsDraft = true, IsActive = false, Version = 1 };
        Apply(entity, req);

        var duplicate = await HasActiveDuplicateAsync(entity.Code, 0, ct);
        var evaluation = SubtestDefinitionValidator.Validate(entity, duplicate);
        if (evaluation.IsHardStopped)
        {
            return EvaluationResult<SubtestDefinitionDto>.Blocked(evaluation);
        }

        await _repo.AddAsync(entity, ct);
        await UnitOfWork.SaveChangesAsync(ct);

        RecordChange(EntityType, entity.Id, entity.Version, ConfigChangeAction.Create, AuditEventType.Create,
            oldValue: null, newValue: Map(entity), reason: req.ChangeReason);
        await UnitOfWork.SaveChangesAsync(ct);

        return EvaluationResult<SubtestDefinitionDto>.Ok(Map(entity), evaluation);
    }

    public async Task<EvaluationResult<SubtestDefinitionDto>> UpdateAsync(long id, SaveSubtestDefinitionRequest req, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(req);

        var entity = await _repo.GetByIdAsync(id, ct);
        if (entity is null)
        {
            return EvaluationResult<SubtestDefinitionDto>.Fail("Subtest definition not found.");
        }

        if (entity.IsActive && string.IsNullOrWhiteSpace(req.ChangeReason))
        {
            return EvaluationResult<SubtestDefinitionDto>.Fail("A change reason is required to edit an active subtest definition.");
        }

        var before = Map(entity);
        Apply(entity, req);
        if (entity.IsActive)
        {
            entity.Version += 1;
        }

        var duplicate = await HasActiveDuplicateAsync(entity.Code, entity.Id, ct);
        var evaluation = SubtestDefinitionValidator.Validate(entity, duplicate);
        if (evaluation.IsHardStopped)
        {
            return EvaluationResult<SubtestDefinitionDto>.Blocked(evaluation);
        }

        _repo.Update(entity);
        RecordChange(EntityType, entity.Id, entity.Version, ConfigChangeAction.Update, AuditEventType.Update,
            oldValue: before, newValue: Map(entity), reason: req.ChangeReason);
        await UnitOfWork.SaveChangesAsync(ct);

        return EvaluationResult<SubtestDefinitionDto>.Ok(Map(entity), evaluation);
    }

    public async Task<EvaluationResult<SubtestDefinitionDto>> ActivateAsync(long id, string? reason, CancellationToken ct = default)
    {
        var entity = await _repo.GetByIdAsync(id, ct);
        if (entity is null)
        {
            return EvaluationResult<SubtestDefinitionDto>.Fail("Subtest definition not found.");
        }

        var duplicate = await HasActiveDuplicateAsync(entity.Code, entity.Id, ct);
        var evaluation = SubtestDefinitionValidator.Validate(entity, duplicate);
        if (evaluation.IsHardStopped)
        {
            return EvaluationResult<SubtestDefinitionDto>.Blocked(evaluation);
        }

        entity.IsActive = true;
        entity.IsDraft = false;
        entity.RetiredUtc = null;
        entity.EffectiveUtc ??= Clock.UtcNow;
        entity.ChangeReason = reason;
        _repo.Update(entity);

        RecordChange(EntityType, entity.Id, entity.Version, ConfigChangeAction.Activate, AuditEventType.Activate,
            oldValue: null, newValue: Map(entity), reason: reason);
        await UnitOfWork.SaveChangesAsync(ct);

        return EvaluationResult<SubtestDefinitionDto>.Ok(Map(entity), evaluation);
    }

    public async Task<OperationResult<SubtestDefinitionDto>> DeactivateAsync(long id, string? reason, CancellationToken ct = default)
    {
        var entity = await _repo.GetByIdAsync(id, ct);
        if (entity is null)
        {
            return OperationResult<SubtestDefinitionDto>.Fail("Subtest definition not found.");
        }

        entity.IsActive = false;
        entity.RetiredUtc = Clock.UtcNow;
        entity.ChangeReason = reason;
        _repo.Update(entity);

        RecordChange(EntityType, entity.Id, entity.Version, ConfigChangeAction.Deactivate, AuditEventType.Deactivate,
            oldValue: null, newValue: Map(entity), reason: reason);
        await UnitOfWork.SaveChangesAsync(ct);

        return OperationResult<SubtestDefinitionDto>.Ok(Map(entity));
    }

    private async Task<bool> HasActiveDuplicateAsync(string code, long selfId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return false;
        }

        var normalized = code.Trim();
        return await _repo.AnyAsync(s => s.IsActive && s.Id != selfId && s.Code == normalized, ct);
    }

    private static void Apply(SubtestDefinition e, SaveSubtestDefinitionRequest req)
    {
        e.Code = (req.Code ?? string.Empty).Trim();
        e.Name = req.Name?.Trim() ?? string.Empty;
        e.ResultType = req.ResultType;
        e.ChoicesJson = req.ResultType == SubtestResultType.FreeText
            ? null
            : SubtestChoiceDefinitions.ToJson(MapChoices(req.Choices));
        e.ChangeReason = req.ChangeReason;
    }

    private static IReadOnlyList<SubtestChoiceDefinition> MapChoices(IReadOnlyList<SubtestChoiceDto>? items) =>
        items?.Select(c => new SubtestChoiceDefinition(c.Code.Trim(), c.Label.Trim(), c.Polarity)).ToList() ?? [];

    private static SubtestDefinitionDto Map(SubtestDefinition s) => new(
        s.Id,
        s.Code,
        s.Name,
        s.ResultType,
        SubtestChoiceDefinitions.Parse(s.ChoicesJson)
            .Select(c => new SubtestChoiceDto(c.Code, c.Label, c.Polarity))
            .ToList(),
        s.Version,
        s.IsActive,
        s.IsDraft,
        s.EffectiveUtc,
        s.RetiredUtc,
        s.ChangeReason);
}
