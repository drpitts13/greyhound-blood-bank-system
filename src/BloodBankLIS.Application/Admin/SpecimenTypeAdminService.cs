using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Common;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Domain.Rules.Config;
using BloodBankLIS.Domain.ValueObjects;

namespace BloodBankLIS.Application.Admin;

public sealed class SpecimenTypeAdminService : ConfigAdminServiceBase
{
    private const string EntityType = nameof(SpecimenTypeDefinition);

    private readonly IRepository<SpecimenTypeDefinition> _repo;
    private readonly IRepository<TestDefinition> _testRepo;

    public SpecimenTypeAdminService(
        IRepository<SpecimenTypeDefinition> repo,
        IRepository<TestDefinition> testRepo,
        IUnitOfWork unitOfWork,
        IClock clock,
        ICurrentUser currentUser,
        IAuditWriter audit,
        IConfigurationHistoryWriter history)
        : base(unitOfWork, clock, currentUser, audit, history)
    {
        _repo = repo;
        _testRepo = testRepo;
    }

    public async Task<IReadOnlyList<SpecimenTypeDefinitionDto>> ListAsync(bool includeInactive, CancellationToken ct = default)
    {
        var items = includeInactive
            ? await _repo.ListAsync(ct)
            : await _repo.ListAsync(d => d.IsActive, ct);
        return items.OrderBy(d => d.SortOrder).ThenBy(d => d.Code).Select(SpecimenTypeDefinitionDtoMapping.From).ToList();
    }

    public async Task<SpecimenTypeDefinitionDto?> GetAsync(long id, CancellationToken ct = default)
    {
        var item = await _repo.GetByIdAsync(id, ct);
        return item is null ? null : SpecimenTypeDefinitionDtoMapping.From(item);
    }

    public async Task<EvaluationResult<SpecimenTypeDefinitionDto>> CreateAsync(SaveSpecimenTypeDefinitionRequest req, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(req);

        var entity = new SpecimenTypeDefinition { IsDraft = true, IsActive = false, Version = 1 };
        Apply(entity, req);

        var duplicate = await HasActiveDuplicateAsync(entity.Code, 0, ct);
        var activeTests = await LoadActiveTestCodesAsync(ct);
        var evaluation = SpecimenTypeDefinitionValidator.Validate(entity, duplicate, activeTests);
        if (evaluation.IsHardStopped)
        {
            return EvaluationResult<SpecimenTypeDefinitionDto>.Blocked(evaluation);
        }

        await _repo.AddAsync(entity, ct);
        await UnitOfWork.SaveChangesAsync(ct);

        RecordChange(EntityType, entity.Id, entity.Version, ConfigChangeAction.Create, AuditEventType.Create,
            oldValue: null, newValue: SpecimenTypeDefinitionDtoMapping.From(entity), reason: req.ChangeReason);
        await UnitOfWork.SaveChangesAsync(ct);

        return EvaluationResult<SpecimenTypeDefinitionDto>.Ok(SpecimenTypeDefinitionDtoMapping.From(entity), evaluation);
    }

    public async Task<EvaluationResult<SpecimenTypeDefinitionDto>> UpdateAsync(long id, SaveSpecimenTypeDefinitionRequest req, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(req);

        var entity = await _repo.GetByIdAsync(id, ct);
        if (entity is null)
        {
            return EvaluationResult<SpecimenTypeDefinitionDto>.Fail("Specimen type definition not found.");
        }

        if (entity.IsActive && string.IsNullOrWhiteSpace(req.ChangeReason))
        {
            return EvaluationResult<SpecimenTypeDefinitionDto>.Fail("A change reason is required to edit an active specimen type definition.");
        }

        var before = SpecimenTypeDefinitionDtoMapping.From(entity);
        Apply(entity, req);
        if (entity.IsActive)
        {
            entity.Version += 1;
        }

        var duplicate = await HasActiveDuplicateAsync(entity.Code, entity.Id, ct);
        var activeTests = await LoadActiveTestCodesAsync(ct);
        var evaluation = SpecimenTypeDefinitionValidator.Validate(entity, duplicate, activeTests);
        if (evaluation.IsHardStopped)
        {
            return EvaluationResult<SpecimenTypeDefinitionDto>.Blocked(evaluation);
        }

        _repo.Update(entity);
        RecordChange(EntityType, entity.Id, entity.Version, ConfigChangeAction.Update, AuditEventType.Update,
            oldValue: before, newValue: SpecimenTypeDefinitionDtoMapping.From(entity), reason: req.ChangeReason);
        await UnitOfWork.SaveChangesAsync(ct);

        return EvaluationResult<SpecimenTypeDefinitionDto>.Ok(SpecimenTypeDefinitionDtoMapping.From(entity), evaluation);
    }

    public async Task<EvaluationResult<SpecimenTypeDefinitionDto>> ActivateAsync(long id, string? reason, CancellationToken ct = default)
    {
        var entity = await _repo.GetByIdAsync(id, ct);
        if (entity is null)
        {
            return EvaluationResult<SpecimenTypeDefinitionDto>.Fail("Specimen type definition not found.");
        }

        var duplicate = await HasActiveDuplicateAsync(entity.Code, entity.Id, ct);
        var activeTests = await LoadActiveTestCodesAsync(ct);
        var evaluation = SpecimenTypeDefinitionValidator.Validate(entity, duplicate, activeTests);
        if (evaluation.IsHardStopped)
        {
            return EvaluationResult<SpecimenTypeDefinitionDto>.Blocked(evaluation);
        }

        entity.IsActive = true;
        entity.IsDraft = false;
        entity.RetiredUtc = null;
        entity.EffectiveUtc ??= Clock.UtcNow;
        entity.ChangeReason = reason;
        _repo.Update(entity);

        RecordChange(EntityType, entity.Id, entity.Version, ConfigChangeAction.Activate, AuditEventType.Activate,
            oldValue: null, newValue: SpecimenTypeDefinitionDtoMapping.From(entity), reason: reason);
        await UnitOfWork.SaveChangesAsync(ct);

        return EvaluationResult<SpecimenTypeDefinitionDto>.Ok(SpecimenTypeDefinitionDtoMapping.From(entity), evaluation);
    }

    public async Task<OperationResult<SpecimenTypeDefinitionDto>> DeactivateAsync(long id, string? reason, CancellationToken ct = default)
    {
        var entity = await _repo.GetByIdAsync(id, ct);
        if (entity is null)
        {
            return OperationResult<SpecimenTypeDefinitionDto>.Fail("Specimen type definition not found.");
        }

        entity.IsActive = false;
        entity.RetiredUtc = Clock.UtcNow;
        entity.ChangeReason = reason;
        _repo.Update(entity);

        RecordChange(EntityType, entity.Id, entity.Version, ConfigChangeAction.Deactivate, AuditEventType.Deactivate,
            oldValue: null, newValue: SpecimenTypeDefinitionDtoMapping.From(entity), reason: reason);
        await UnitOfWork.SaveChangesAsync(ct);

        return OperationResult<SpecimenTypeDefinitionDto>.Ok(SpecimenTypeDefinitionDtoMapping.From(entity));
    }

    private async Task<HashSet<string>> LoadActiveTestCodesAsync(CancellationToken ct)
    {
        var tests = await _testRepo.ListAsync(t => t.IsActive && !t.IsDraft, ct);
        return tests.Select(t => t.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
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

    private static void Apply(SpecimenTypeDefinition e, SaveSpecimenTypeDefinitionRequest req)
    {
        e.Code = (req.Code ?? string.Empty).Trim().ToUpperInvariant();
        e.Description = req.Description?.Trim() ?? string.Empty;
        e.ExcludedTestCodesJson = SpecimenTypeExcludedTests.Serialize(req.ExcludedTestCodes ?? []);
        e.SortOrder = req.SortOrder;
        e.ChangeReason = req.ChangeReason;
    }
}
