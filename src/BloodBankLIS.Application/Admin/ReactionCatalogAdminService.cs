using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Common;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Application.Admin;

public sealed record ReactionCatalogEntryDto(long Id, ReactionCatalogKind Kind, string Code, string Text, int SortOrder, bool IsActive, bool IsNoneOption)
{
    public static ReactionCatalogEntryDto From(ReactionCatalogEntry e) =>
        new(e.Id, e.Kind, e.Code, e.Text, e.SortOrder, e.IsActive, e.IsNoneOption);
}

public sealed record SaveReactionCatalogEntryRequest(ReactionCatalogKind Kind, string Code, string Text, int SortOrder);

/// <summary>
/// OCD-057: the admin lists of reactions a provider may conclude and of treatments for future transfusions. Changes
/// are audited with configuration history. No reaction and No treatment cannot be deactivated or recoded.
/// </summary>
public sealed class ReactionCatalogAdminService : ConfigAdminServiceBase
{
    private const string EntityType = nameof(ReactionCatalogEntry);

    private readonly IRepository<ReactionCatalogEntry> _entries;
    private readonly IPermissionEvaluator? _permissionEvaluator;

    public ReactionCatalogAdminService(
        IRepository<ReactionCatalogEntry> entries,
        IUnitOfWork unitOfWork,
        IClock clock,
        ICurrentUser currentUser,
        IAuditWriter audit,
        IConfigurationHistoryWriter history,
        IPermissionEvaluator? permissionEvaluator = null)
        : base(unitOfWork, clock, currentUser, audit, history)
    {
        _entries = entries;
        _permissionEvaluator = permissionEvaluator;
    }

    public async Task<IReadOnlyList<ReactionCatalogEntryDto>> ListAsync(ReactionCatalogKind? kind, bool includeInactive, CancellationToken ct = default)
    {
        var rows = await _entries.ListAsync(ct);
        return rows
            .Where(e => (kind is null || e.Kind == kind) && (includeInactive || e.IsActive))
            .OrderBy(e => e.Kind).ThenBy(e => e.SortOrder).ThenBy(e => e.Code)
            .Select(ReactionCatalogEntryDto.From)
            .ToList();
    }

    public async Task<EvaluationResult<ReactionCatalogEntryDto>> CreateAsync(SaveReactionCatalogEntryRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var denied = await RejectUnauthorizedAsync(PermissionCodes.AdminConfigEdit, ct);
        if (denied is not null)
        {
            return denied;
        }

        var code = ReactionConclusionRule.Normalize(request.Code);
        var invalid = await ValidateAsync(request.Kind, code, request.Text, excludeId: null, ct);
        if (invalid is not null)
        {
            return invalid;
        }

        var entity = new ReactionCatalogEntry
        {
            Kind = request.Kind, Code = code, Text = request.Text.Trim(), SortOrder = request.SortOrder, IsActive = true
        };
        await _entries.AddAsync(entity, ct);
        await UnitOfWork.SaveChangesAsync(ct);
        var dto = ReactionCatalogEntryDto.From(entity);
        RecordChange(EntityType, entity.Id, 1, ConfigChangeAction.Create, AuditEventType.Configure, null, dto, null);
        await UnitOfWork.SaveChangesAsync(ct);
        return EvaluationResult<ReactionCatalogEntryDto>.Ok(dto);
    }

    public async Task<EvaluationResult<ReactionCatalogEntryDto>> UpdateAsync(long id, SaveReactionCatalogEntryRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var denied = await RejectUnauthorizedAsync(PermissionCodes.AdminConfigEdit, ct);
        if (denied is not null)
        {
            return denied;
        }

        var entity = await _entries.GetByIdAsync(id, ct);
        if (entity is null)
        {
            return EvaluationResult<ReactionCatalogEntryDto>.Fail("List entry not found.");
        }

        var code = ReactionConclusionRule.Normalize(request.Code);
        var fixedNone = ReactionConclusionRule.EvaluateCatalogChange(entity.IsNoneOption, code, null);
        if (fixedNone.Severity == RuleSeverity.HardStop)
        {
            return EvaluationResult<ReactionCatalogEntryDto>.Blocked(new RuleEvaluation([fixedNone]));
        }

        var invalid = await ValidateAsync(entity.Kind, code, request.Text, id, ct);
        if (invalid is not null)
        {
            return invalid;
        }

        var old = ReactionCatalogEntryDto.From(entity);
        entity.Code = code;
        entity.Text = request.Text.Trim();
        entity.SortOrder = request.SortOrder;
        _entries.Update(entity);
        var dto = ReactionCatalogEntryDto.From(entity);
        RecordChange(EntityType, entity.Id, 1, ConfigChangeAction.Update, AuditEventType.Configure, old, dto, null);
        await UnitOfWork.SaveChangesAsync(ct);
        return EvaluationResult<ReactionCatalogEntryDto>.Ok(dto);
    }

    public async Task<EvaluationResult<ReactionCatalogEntryDto>> SetActiveAsync(long id, bool active, CancellationToken ct = default)
    {
        var denied = await RejectUnauthorizedAsync(PermissionCodes.AdminConfigActivate, ct);
        if (denied is not null)
        {
            return denied;
        }

        var entity = await _entries.GetByIdAsync(id, ct);
        if (entity is null)
        {
            return EvaluationResult<ReactionCatalogEntryDto>.Fail("List entry not found.");
        }

        var fixedNone = ReactionConclusionRule.EvaluateCatalogChange(entity.IsNoneOption, null, active);
        if (fixedNone.Severity == RuleSeverity.HardStop)
        {
            return EvaluationResult<ReactionCatalogEntryDto>.Blocked(new RuleEvaluation([fixedNone]));
        }

        var old = ReactionCatalogEntryDto.From(entity);
        entity.IsActive = active;
        _entries.Update(entity);
        var dto = ReactionCatalogEntryDto.From(entity);
        var action = active ? ConfigChangeAction.Activate : ConfigChangeAction.Deactivate;
        RecordChange(EntityType, entity.Id, 1, action, active ? AuditEventType.Activate : AuditEventType.Deactivate, old, dto, null);
        await UnitOfWork.SaveChangesAsync(ct);
        return EvaluationResult<ReactionCatalogEntryDto>.Ok(dto);
    }

    private async Task<EvaluationResult<ReactionCatalogEntryDto>?> ValidateAsync(
        ReactionCatalogKind kind, string code, string? text, long? excludeId, CancellationToken ct)
    {
        if (code.Length == 0 || string.IsNullOrWhiteSpace(text))
        {
            return EvaluationResult<ReactionCatalogEntryDto>.Blocked(new RuleEvaluation([
                RuleResult.HardStop(ReactionConclusionRule.Code, "A code and the text shown to the provider are required.")]));
        }

        if (code == ReactionConclusionRule.NoneCode && excludeId is null)
        {
            return EvaluationResult<ReactionCatalogEntryDto>.Blocked(new RuleEvaluation([
                RuleResult.HardStop(ReactionConclusionRule.Code, "NONE is reserved for No reaction and No treatment.")]));
        }

        var duplicate = await _entries.AnyAsync(e => e.Kind == kind && e.Code == code && (excludeId == null || e.Id != excludeId), ct);
        return duplicate
            ? EvaluationResult<ReactionCatalogEntryDto>.Blocked(new RuleEvaluation([
                RuleResult.HardStop(ReactionConclusionRule.Code, $"Code {code} is already on this list.")]))
            : null;
    }

    private async Task<EvaluationResult<ReactionCatalogEntryDto>?> RejectUnauthorizedAsync(string permissionCode, CancellationToken ct)
    {
        if (_permissionEvaluator is null)
        {
            return null;
        }

        var allowed = await _permissionEvaluator.HasPermissionAsync(CurrentUser.UserName, permissionCode, ct);
        return allowed
            ? null
            : EvaluationResult<ReactionCatalogEntryDto>.Blocked(new RuleEvaluation([
                RuleResult.HardStop(ReactionConclusionRule.Code, $"Changing the reaction lists requires the {permissionCode} permission.")]));
    }
}
