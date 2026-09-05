using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Common;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Application.Admin;

public sealed record CompatibilityRuleVersionDto(
    long Id,
    string Version,
    string PolicyVersion,
    DateOnly EffectiveDate,
    DateOnly? RetiredDate,
    bool IsActive,
    string Notes,
    int RuleCount)
{
    public static CompatibilityRuleVersionDto From(CompatibilityRuleVersion v, int ruleCount) => new(
        v.Id, v.Version, v.PolicyVersion, v.EffectiveDate, v.RetiredDate, v.IsActive, v.Notes, ruleCount);
}

public sealed record CompatibilityRuleDto(
    long Id,
    long CompatibilityRuleVersionId,
    string RuleCode,
    ComponentClass ComponentClass,
    string RuleFamily,
    string ExpressionJson,
    string Severity,
    bool IsActive,
    string Description)
{
    public static CompatibilityRuleDto From(CompatibilityRule r) => new(
        r.Id, r.CompatibilityRuleVersionId, r.RuleCode, r.ComponentClass, r.RuleFamily,
        r.ExpressionJson, r.Severity, r.IsActive, r.Description);
}

public sealed record SaveCompatibilityRuleVersionRequest(
    string Version,
    string PolicyVersion,
    DateOnly EffectiveDate,
    string Notes,
    string? Reason = null);

public sealed record SaveCompatibilityRuleRequest(
    string RuleCode,
    ComponentClass ComponentClass,
    string? RuleFamily,
    string? ExpressionJson,
    string Severity,
    string Description);

public sealed class CompatibilityRuleAdminService : ConfigAdminServiceBase
{
    private const string VersionEntity = nameof(CompatibilityRuleVersion);
    private const string RuleEntity = nameof(CompatibilityRule);

    private readonly IRepository<CompatibilityRuleVersion> _versions;
    private readonly IRepository<CompatibilityRule> _rules;
    private readonly IPermissionEvaluator? _permissionEvaluator;

    public CompatibilityRuleAdminService(
        IRepository<CompatibilityRuleVersion> versions,
        IRepository<CompatibilityRule> rules,
        IUnitOfWork unitOfWork,
        IClock clock,
        ICurrentUser currentUser,
        IAuditWriter audit,
        IConfigurationHistoryWriter history,
        IPermissionEvaluator? permissionEvaluator = null)
        : base(unitOfWork, clock, currentUser, audit, history)
    {
        _versions = versions;
        _rules = rules;
        _permissionEvaluator = permissionEvaluator;
    }

    public async Task<IReadOnlyList<CompatibilityRuleVersionDto>> ListVersionsAsync(
        bool includeInactive,
        CancellationToken ct = default)
    {
        var versions = includeInactive
            ? await _versions.ListAsync(ct)
            : await _versions.ListAsync(v => v.IsActive, ct);
        var rules = await _rules.ListAsync(ct);
        var counts = rules.GroupBy(r => r.CompatibilityRuleVersionId).ToDictionary(g => g.Key, g => g.Count());
        return versions
            .OrderByDescending(v => v.IsActive)
            .ThenByDescending(v => v.EffectiveDate)
            .ThenBy(v => v.Version)
            .Select(v => CompatibilityRuleVersionDto.From(v, counts.GetValueOrDefault(v.Id)))
            .ToList();
    }

    public async Task<CompatibilityRuleVersionDto?> GetVersionAsync(long id, CancellationToken ct = default)
    {
        var entity = await _versions.GetByIdAsync(id, ct);
        if (entity is null)
        {
            return null;
        }

        var count = (await _rules.ListAsync(r => r.CompatibilityRuleVersionId == id, ct)).Count;
        return CompatibilityRuleVersionDto.From(entity, count);
    }

    public IReadOnlyList<CompatibilityRuleDefinition> ListCatalog() => CompatibilityRuleCatalog.Defaults;

    public async Task<IReadOnlyList<CompatibilityRuleDto>> ListRulesAsync(long versionId, CancellationToken ct = default)
    {
        var list = await _rules.ListAsync(r => r.CompatibilityRuleVersionId == versionId, ct);
        return list.OrderBy(r => r.ComponentClass).ThenBy(r => r.RuleCode).Select(CompatibilityRuleDto.From).ToList();
    }

    public async Task<EvaluationResult<CompatibilityRuleVersionDto>> CreateVersionAsync(
        SaveCompatibilityRuleVersionRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var denied = await RejectUnauthorizedVersionAsync(
            PermissionCodes.AdminConfigEdit, CompatibilityTableAuthorizationRule.EvaluateCreateVersion, ct);
        if (denied is not null)
        {
            return denied;
        }

        var entity = ApplyVersion(new CompatibilityRuleVersion { IsActive = false }, request);
        var duplicate = await _versions.AnyAsync(v => v.Version == entity.Version, ct);
        var validation = CompatibilityRuleValidator.ValidateVersion(entity, duplicate, request.Reason, requireReason: false);
        if (validation.IsHardStopped)
        {
            return EvaluationResult<CompatibilityRuleVersionDto>.Blocked(validation);
        }

        await _versions.AddAsync(entity, ct);
        await UnitOfWork.SaveChangesAsync(ct);
        var dto = CompatibilityRuleVersionDto.From(entity, 0);
        RecordChange(VersionEntity, entity.Id, 1, ConfigChangeAction.Create, AuditEventType.Configure, null, dto, request.Reason);
        await UnitOfWork.SaveChangesAsync(ct);
        return EvaluationResult<CompatibilityRuleVersionDto>.Ok(dto, validation);
    }

    public async Task<EvaluationResult<CompatibilityRuleVersionDto>> UpdateVersionAsync(
        long id,
        SaveCompatibilityRuleVersionRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var denied = await RejectUnauthorizedVersionAsync(
            PermissionCodes.AdminConfigEdit, CompatibilityTableAuthorizationRule.EvaluateUpdateVersion, ct);
        if (denied is not null)
        {
            return denied;
        }

        var entity = await _versions.GetByIdAsync(id, ct);
        if (entity is null)
        {
            return EvaluationResult<CompatibilityRuleVersionDto>.Fail("Compatibility table version not found.");
        }

        var old = await ToDtoAsync(entity, ct);
        ApplyVersion(entity, request);
        var duplicate = await _versions.AnyAsync(v => v.Version == entity.Version && v.Id != id, ct);
        var validation = CompatibilityRuleValidator.ValidateVersion(entity, duplicate, request.Reason, requireReason: true);
        if (validation.IsHardStopped)
        {
            return EvaluationResult<CompatibilityRuleVersionDto>.Blocked(validation);
        }

        _versions.Update(entity);
        var dto = await ToDtoAsync(entity, ct);
        RecordChange(VersionEntity, entity.Id, 1, ConfigChangeAction.Update, AuditEventType.Configure, old, dto, request.Reason!.Trim());
        await UnitOfWork.SaveChangesAsync(ct);
        return EvaluationResult<CompatibilityRuleVersionDto>.Ok(dto, validation);
    }

    public async Task<EvaluationResult<CompatibilityRuleVersionDto>> ActivateVersionAsync(
        long id,
        string? reason,
        CancellationToken ct = default)
    {
        var denied = await RejectUnauthorizedVersionAsync(
            PermissionCodes.AdminConfigActivate, CompatibilityTableAuthorizationRule.EvaluateActivateVersion, ct);
        if (denied is not null)
        {
            return denied;
        }

        var entity = await _versions.GetByIdAsync(id, ct);
        if (entity is null)
        {
            return EvaluationResult<CompatibilityRuleVersionDto>.Fail("Compatibility table version not found.");
        }

        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length < 8)
        {
            return EvaluationResult<CompatibilityRuleVersionDto>.Blocked(new RuleEvaluation([
                RuleResult.HardStop(CompatibilityRuleValidator.ReasonCode, "A change reason of at least 8 characters is required to activate a compatibility table.")]));
        }

        var otherIds = (await _versions.ListAsync(v => v.IsActive && v.Id != id, ct)).Select(v => v.Id).ToList();
        foreach (var otherId in otherIds)
        {
            var other = await _versions.GetByIdAsync(otherId, ct);
            if (other is null)
            {
                continue;
            }

            other.IsActive = false;
            other.RetiredDate ??= DateOnly.FromDateTime(Clock.UtcNow);
        }

        var old = await ToDtoAsync(entity, ct);
        entity.IsActive = true;
        entity.RetiredDate = null;
        var dto = await ToDtoAsync(entity, ct);
        RecordChange(VersionEntity, entity.Id, 1, ConfigChangeAction.Activate, AuditEventType.Activate, old, dto, reason.Trim());
        await UnitOfWork.SaveChangesAsync(ct);
        return EvaluationResult<CompatibilityRuleVersionDto>.Ok(dto, new RuleEvaluation([]));
    }

    public async Task<EvaluationResult<CompatibilityRuleVersionDto>> RetireVersionAsync(
        long id,
        string? reason,
        CancellationToken ct = default)
    {
        var denied = await RejectUnauthorizedVersionAsync(
            PermissionCodes.AdminConfigActivate, CompatibilityTableAuthorizationRule.EvaluateRetireVersion, ct);
        if (denied is not null)
        {
            return denied;
        }

        var entity = await _versions.GetByIdAsync(id, ct);
        if (entity is null)
        {
            return EvaluationResult<CompatibilityRuleVersionDto>.Fail("Compatibility table version not found.");
        }

        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length < 8)
        {
            return EvaluationResult<CompatibilityRuleVersionDto>.Blocked(new RuleEvaluation([
                RuleResult.HardStop(CompatibilityRuleValidator.ReasonCode, "A change reason of at least 8 characters is required to retire a compatibility table.")]));
        }

        var old = await ToDtoAsync(entity, ct);
        entity.IsActive = false;
        entity.RetiredDate ??= DateOnly.FromDateTime(Clock.UtcNow);
        _versions.Update(entity);
        var dto = await ToDtoAsync(entity, ct);
        RecordChange(VersionEntity, entity.Id, 1, ConfigChangeAction.Deactivate, AuditEventType.Deactivate, old, dto, reason.Trim());
        await UnitOfWork.SaveChangesAsync(ct);
        return EvaluationResult<CompatibilityRuleVersionDto>.Ok(dto, new RuleEvaluation([]));
    }

    public async Task<EvaluationResult<CompatibilityRuleDto>> CreateRuleAsync(
        long versionId,
        SaveCompatibilityRuleRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var denied = await RejectUnauthorizedRuleAsync(
            PermissionCodes.AdminConfigEdit, CompatibilityTableAuthorizationRule.EvaluateCreateRule, ct);
        if (denied is not null)
        {
            return denied;
        }

        var versionExists = await _versions.AnyAsync(v => v.Id == versionId, ct);
        var entity = ApplyRule(new CompatibilityRule { CompatibilityRuleVersionId = versionId, IsActive = true }, request);
        var duplicate = await _rules.AnyAsync(
            r => r.CompatibilityRuleVersionId == versionId && r.RuleCode == entity.RuleCode, ct);
        var validation = CompatibilityRuleValidator.ValidateRule(entity, versionExists, duplicate);
        if (validation.IsHardStopped)
        {
            return EvaluationResult<CompatibilityRuleDto>.Blocked(validation);
        }

        await _rules.AddAsync(entity, ct);
        await UnitOfWork.SaveChangesAsync(ct);
        var dto = CompatibilityRuleDto.From(entity);
        RecordChange(RuleEntity, entity.Id, 1, ConfigChangeAction.Create, AuditEventType.Configure, null, dto, null);
        await UnitOfWork.SaveChangesAsync(ct);
        return EvaluationResult<CompatibilityRuleDto>.Ok(dto, validation);
    }

    public async Task<EvaluationResult<CompatibilityRuleDto>> UpdateRuleAsync(
        long id,
        SaveCompatibilityRuleRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var denied = await RejectUnauthorizedRuleAsync(
            PermissionCodes.AdminConfigEdit, CompatibilityTableAuthorizationRule.EvaluateUpdateRule, ct);
        if (denied is not null)
        {
            return denied;
        }

        var entity = await _rules.GetByIdAsync(id, ct);
        if (entity is null)
        {
            return EvaluationResult<CompatibilityRuleDto>.Fail("Compatibility rule not found.");
        }

        var old = CompatibilityRuleDto.From(entity);
        ApplyRule(entity, request);
        var duplicate = await _rules.AnyAsync(
            r => r.CompatibilityRuleVersionId == entity.CompatibilityRuleVersionId
                 && r.RuleCode == entity.RuleCode
                 && r.Id != id, ct);
        var validation = CompatibilityRuleValidator.ValidateRule(entity, true, duplicate);
        if (validation.IsHardStopped)
        {
            return EvaluationResult<CompatibilityRuleDto>.Blocked(validation);
        }

        _rules.Update(entity);
        var dto = CompatibilityRuleDto.From(entity);
        RecordChange(RuleEntity, entity.Id, 1, ConfigChangeAction.Update, AuditEventType.Configure, old, dto, null);
        await UnitOfWork.SaveChangesAsync(ct);
        return EvaluationResult<CompatibilityRuleDto>.Ok(dto, validation);
    }

    public async Task<EvaluationResult<CompatibilityRuleDto>> SetRuleActiveAsync(
        long id,
        bool active,
        CancellationToken ct = default)
    {
        var denied = await RejectUnauthorizedRuleAsync(
            PermissionCodes.AdminConfigActivate,
            active
                ? CompatibilityTableAuthorizationRule.EvaluateActivateRule
                : CompatibilityTableAuthorizationRule.EvaluateDeactivateRule,
            ct);
        if (denied is not null)
        {
            return denied;
        }

        var entity = await _rules.GetByIdAsync(id, ct);
        if (entity is null)
        {
            return EvaluationResult<CompatibilityRuleDto>.Fail("Compatibility rule not found.");
        }

        var old = CompatibilityRuleDto.From(entity);
        entity.IsActive = active;
        _rules.Update(entity);
        var action = active ? ConfigChangeAction.Activate : ConfigChangeAction.Deactivate;
        RecordChange(RuleEntity, entity.Id, 1, action, ToAuditType(action), old, CompatibilityRuleDto.From(entity), null);
        await UnitOfWork.SaveChangesAsync(ct);
        return EvaluationResult<CompatibilityRuleDto>.Ok(CompatibilityRuleDto.From(entity), new RuleEvaluation([]));
    }

    private async Task<CompatibilityRuleVersionDto> ToDtoAsync(CompatibilityRuleVersion entity, CancellationToken ct)
    {
        var count = (await _rules.ListAsync(r => r.CompatibilityRuleVersionId == entity.Id, ct)).Count;
        return CompatibilityRuleVersionDto.From(entity, count);
    }

    private static CompatibilityRuleVersion ApplyVersion(CompatibilityRuleVersion entity, SaveCompatibilityRuleVersionRequest request)
    {
        entity.Version = request.Version.Trim();
        entity.PolicyVersion = request.PolicyVersion.Trim();
        entity.EffectiveDate = request.EffectiveDate;
        entity.Notes = string.IsNullOrWhiteSpace(request.Notes)
            ? "INSTITUTIONAL_POLICY_REVIEW required."
            : request.Notes.Trim();
        return entity;
    }

    private static CompatibilityRule ApplyRule(CompatibilityRule entity, SaveCompatibilityRuleRequest request)
    {
        var catalog = CompatibilityRuleCatalog.Find(request.RuleCode);
        entity.RuleCode = request.RuleCode.Trim().ToUpperInvariant();
        entity.ComponentClass = request.ComponentClass;
        entity.RuleFamily = string.IsNullOrWhiteSpace(request.RuleFamily)
            ? catalog?.RuleFamily ?? request.ComponentClass.ToString()
            : request.RuleFamily.Trim();
        entity.ExpressionJson = string.IsNullOrWhiteSpace(request.ExpressionJson) ? "{}" : request.ExpressionJson.Trim();
        entity.Severity = CompatibilityRuleValidator.NormalizeSeverity(request.Severity);
        entity.Description = request.Description.Trim();
        return entity;
    }

    private async Task<EvaluationResult<CompatibilityRuleVersionDto>?> RejectUnauthorizedVersionAsync(
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
            ? EvaluationResult<CompatibilityRuleVersionDto>.Blocked(new RuleEvaluation([auth]))
            : null;
    }

    private async Task<EvaluationResult<CompatibilityRuleDto>?> RejectUnauthorizedRuleAsync(
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
            ? EvaluationResult<CompatibilityRuleDto>.Blocked(new RuleEvaluation([auth]))
            : null;
    }
}
