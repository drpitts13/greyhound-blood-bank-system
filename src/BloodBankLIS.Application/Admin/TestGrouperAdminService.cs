using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Common;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Domain.Rules.Config;
using BloodBankLIS.Domain.ValueObjects;

namespace BloodBankLIS.Application.Admin;

public sealed class TestGrouperAdminService : ConfigAdminServiceBase
{
    private const string EntityType = nameof(TestGrouper);

    private readonly IRepository<TestGrouper> _repo;
    private readonly IRepository<TestDefinition> _testRepo;
    private readonly IPermissionEvaluator? _permissionEvaluator;

    public TestGrouperAdminService(
        IRepository<TestGrouper> repo,
        IRepository<TestDefinition> testRepo,
        IUnitOfWork unitOfWork,
        IClock clock,
        ICurrentUser currentUser,
        IAuditWriter audit,
        IConfigurationHistoryWriter history,
        IPermissionEvaluator? permissionEvaluator = null)
        : base(unitOfWork, clock, currentUser, audit, history)
    {
        _repo = repo;
        _testRepo = testRepo;
        _permissionEvaluator = permissionEvaluator;
    }

    public async Task<IReadOnlyList<TestGrouperDto>> ListAsync(bool includeInactive, CancellationToken ct = default)
    {
        var items = includeInactive
            ? await _repo.ListAsync(ct)
            : await _repo.ListAsync(g => g.IsActive, ct);
        return items.OrderBy(g => g.Code).Select(Map).ToList();
    }

    public async Task<TestGrouperDto?> GetAsync(long id, CancellationToken ct = default)
    {
        var item = await _repo.GetByIdAsync(id, ct);
        return item is null ? null : Map(item);
    }

    public async Task<EvaluationResult<TestGrouperDto>> CreateAsync(SaveTestGrouperRequest req, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(req);

        var denied = await RejectUnauthorizedEvalAsync(
            PermissionCodes.AdminTestsManage, TestGrouperAuthorizationRule.EvaluateCreate, ct);
        if (denied is not null)
        {
            return denied;
        }

        var entity = new TestGrouper { IsDraft = true, IsActive = false, Version = 1 };
        Apply(entity, req);

        var duplicate = await HasActiveDuplicateAsync(entity.Code, 0, ct);
        var activeTests = await LoadActiveTestCodesAsync(ct);
        var evaluation = TestGrouperValidator.Validate(entity, duplicate, activeTests);
        if (evaluation.IsHardStopped)
        {
            return EvaluationResult<TestGrouperDto>.Blocked(evaluation);
        }

        await _repo.AddAsync(entity, ct);
        await UnitOfWork.SaveChangesAsync(ct);

        RecordChange(EntityType, entity.Id, entity.Version, ConfigChangeAction.Create, AuditEventType.Create,
            oldValue: null, newValue: Map(entity), reason: req.ChangeReason);
        await UnitOfWork.SaveChangesAsync(ct);

        return EvaluationResult<TestGrouperDto>.Ok(Map(entity), evaluation);
    }

    public async Task<EvaluationResult<TestGrouperDto>> UpdateAsync(long id, SaveTestGrouperRequest req, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(req);

        var denied = await RejectUnauthorizedEvalAsync(
            PermissionCodes.AdminTestsManage, TestGrouperAuthorizationRule.EvaluateUpdate, ct);
        if (denied is not null)
        {
            return denied;
        }

        var entity = await _repo.GetByIdAsync(id, ct);
        if (entity is null)
        {
            return EvaluationResult<TestGrouperDto>.Fail("Test grouper not found.");
        }

        if (entity.IsActive && string.IsNullOrWhiteSpace(req.ChangeReason))
        {
            return EvaluationResult<TestGrouperDto>.Fail("A change reason is required to edit an active test grouper.");
        }

        var before = Map(entity);
        Apply(entity, req);
        if (entity.IsActive)
        {
            entity.Version += 1;
        }

        var duplicate = await HasActiveDuplicateAsync(entity.Code, entity.Id, ct);
        var activeTests = await LoadActiveTestCodesAsync(ct);
        var evaluation = TestGrouperValidator.Validate(entity, duplicate, activeTests);
        if (evaluation.IsHardStopped)
        {
            return EvaluationResult<TestGrouperDto>.Blocked(evaluation);
        }

        _repo.Update(entity);
        RecordChange(EntityType, entity.Id, entity.Version, ConfigChangeAction.Update, AuditEventType.Update,
            oldValue: before, newValue: Map(entity), reason: req.ChangeReason);
        await UnitOfWork.SaveChangesAsync(ct);

        return EvaluationResult<TestGrouperDto>.Ok(Map(entity), evaluation);
    }

    public async Task<EvaluationResult<TestGrouperDto>> ActivateAsync(long id, string? reason, CancellationToken ct = default)
    {
        var denied = await RejectUnauthorizedEvalAsync(
            PermissionCodes.AdminConfigActivate, TestGrouperAuthorizationRule.EvaluateActivate, ct);
        if (denied is not null)
        {
            return denied;
        }

        var entity = await _repo.GetByIdAsync(id, ct);
        if (entity is null)
        {
            return EvaluationResult<TestGrouperDto>.Fail("Test grouper not found.");
        }

        var duplicate = await HasActiveDuplicateAsync(entity.Code, entity.Id, ct);
        var activeTests = await LoadActiveTestCodesAsync(ct);
        var evaluation = TestGrouperValidator.Validate(entity, duplicate, activeTests);
        if (evaluation.IsHardStopped)
        {
            return EvaluationResult<TestGrouperDto>.Blocked(evaluation);
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

        return EvaluationResult<TestGrouperDto>.Ok(Map(entity), evaluation);
    }

    public async Task<OperationResult<TestGrouperDto>> DeactivateAsync(long id, string? reason, CancellationToken ct = default)
    {
        var denied = await RejectUnauthorizedAsync(
            PermissionCodes.AdminConfigActivate, TestGrouperAuthorizationRule.EvaluateDeactivate, ct);
        if (denied is not null)
        {
            return denied;
        }

        var entity = await _repo.GetByIdAsync(id, ct);
        if (entity is null)
        {
            return OperationResult<TestGrouperDto>.Fail("Test grouper not found.");
        }

        entity.IsActive = false;
        entity.RetiredUtc = Clock.UtcNow;
        entity.ChangeReason = reason;
        _repo.Update(entity);

        RecordChange(EntityType, entity.Id, entity.Version, ConfigChangeAction.Deactivate, AuditEventType.Deactivate,
            oldValue: null, newValue: Map(entity), reason: reason);
        await UnitOfWork.SaveChangesAsync(ct);

        return OperationResult<TestGrouperDto>.Ok(Map(entity));
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

        var normalized = code.Trim().ToUpperInvariant();
        return await _repo.AnyAsync(g => g.IsActive && g.Id != selfId && g.Code == normalized, ct);
    }

    private static void Apply(TestGrouper e, SaveTestGrouperRequest req)
    {
        e.Code = (req.Code ?? string.Empty).Trim().ToUpperInvariant();
        e.Name = req.Name?.Trim() ?? string.Empty;
        e.MemberTestsJson = TestGrouperMembers.ToJson(MapMembers(req.Members));
        e.ChangeReason = req.ChangeReason;
    }

    private async Task<EvaluationResult<TestGrouperDto>?> RejectUnauthorizedEvalAsync(
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
            ? EvaluationResult<TestGrouperDto>.Blocked(new RuleEvaluation([auth]))
            : null;
    }

    private async Task<OperationResult<TestGrouperDto>?> RejectUnauthorizedAsync(
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
            ? OperationResult<TestGrouperDto>.Fail(auth.Message)
            : null;
    }

    private static IReadOnlyList<TestGrouperMember> MapMembers(IReadOnlyList<TestGrouperMemberDto>? items) =>
        items?.Select(m => new TestGrouperMember(m.TestCode.Trim().ToUpperInvariant(), m.SortOrder)).ToList() ?? [];

    private static TestGrouperDto Map(TestGrouper g) => new(
        g.Id,
        g.Code,
        g.Name,
        TestGrouperMembers.Parse(g.MemberTestsJson)
            .Select(m => new TestGrouperMemberDto(m.TestCode, m.SortOrder))
            .ToList(),
        g.Version,
        g.IsActive,
        g.IsDraft,
        g.EffectiveUtc,
        g.RetiredUtc,
        g.ChangeReason);
}
