using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Common;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Domain.Rules.Config;
using BloodBankLIS.Domain.ValueObjects;

namespace BloodBankLIS.Application.Admin;

/// <summary>An active Test Group offered for a reaction workup specimen, with its member tests.</summary>
public sealed record ReactionWorkupGroupOptionDto(string Code, string Name, IReadOnlyList<string> MemberTestCodes);

public sealed record ReactionWorkupSettingsDto(
    long Id,
    string? PreTransfusionTestGroupCode,
    string? PostTransfusionTestGroupCode,
    int Version,
    IReadOnlyList<ReactionWorkupGroupOptionDto> TestGroups);

public sealed record SaveReactionWorkupSettingsRequest(
    string? PreTransfusionTestGroupCode,
    string? PostTransfusionTestGroupCode,
    string Reason);

/// <summary>
/// Maps each transfusion reaction workup specimen (pre-transfusion, post-transfusion) to a Test Group. Versioned,
/// with a required reason and configuration history, like the crossmatch settings.
/// </summary>
public sealed class ReactionWorkupSettingsAdminService : ConfigAdminServiceBase
{
    private const string EntityType = nameof(ReactionWorkupSettings);

    private readonly IRepository<ReactionWorkupSettings> _settings;
    private readonly IRepository<TestGrouper> _groupers;
    private readonly IPermissionEvaluator? _permissionEvaluator;

    public ReactionWorkupSettingsAdminService(
        IRepository<ReactionWorkupSettings> settings,
        IRepository<TestGrouper> groupers,
        IUnitOfWork unitOfWork,
        IClock clock,
        ICurrentUser currentUser,
        IAuditWriter audit,
        IConfigurationHistoryWriter history,
        IPermissionEvaluator? permissionEvaluator = null)
        : base(unitOfWork, clock, currentUser, audit, history)
    {
        _settings = settings;
        _groupers = groupers;
        _permissionEvaluator = permissionEvaluator;
    }

    public async Task<ReactionWorkupSettingsDto> GetAsync(CancellationToken ct = default) =>
        await ToDtoAsync(await GetOrCreateAsync(ct), ct);

    public async Task<EvaluationResult<ReactionWorkupSettingsDto>> UpdateAsync(
        SaveReactionWorkupSettingsRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (_permissionEvaluator is not null)
        {
            var allowed = await _permissionEvaluator.HasPermissionAsync(CurrentUser.UserName, PermissionCodes.AdminConfigEdit, ct);
            var auth = ReactionWorkupSettingsAuthorizationRule.EvaluateUpdate(allowed);
            if (auth.Severity == RuleSeverity.HardStop)
            {
                return EvaluationResult<ReactionWorkupSettingsDto>.Blocked(new RuleEvaluation([auth]));
            }
        }

        var entity = await GetOrCreateAsync(ct);
        var old = await ToDtoAsync(entity, ct);
        entity.PreTransfusionTestGroupCode = NormalizeCode(request.PreTransfusionTestGroupCode);
        entity.PostTransfusionTestGroupCode = NormalizeCode(request.PostTransfusionTestGroupCode);

        var validation = ReactionWorkupSettingsValidator.Validate(
            entity,
            request.Reason,
            await FindReleasedGroupAsync(entity.PreTransfusionTestGroupCode, ct),
            await FindReleasedGroupAsync(entity.PostTransfusionTestGroupCode, ct));
        if (validation.IsHardStopped)
        {
            return EvaluationResult<ReactionWorkupSettingsDto>.Blocked(validation);
        }

        entity.Version += 1;
        entity.ChangeReason = request.Reason.Trim();
        entity.IsActive = true;
        entity.IsDraft = false;
        entity.EffectiveUtc ??= Clock.UtcNow;
        _settings.Update(entity);

        var dto = await ToDtoAsync(entity, ct);
        RecordChange(EntityType, entity.Id, entity.Version, ConfigChangeAction.Update, AuditEventType.Configure, old, dto, request.Reason.Trim());
        await UnitOfWork.SaveChangesAsync(ct);
        return EvaluationResult<ReactionWorkupSettingsDto>.Ok(dto, validation);
    }

    private async Task<ReactionWorkupSettings> GetOrCreateAsync(CancellationToken ct)
    {
        var listed = (await _settings.ListAsync(ct))
            .OrderByDescending(s => s.IsActive)
            .ThenByDescending(s => s.Version)
            .FirstOrDefault();
        if (listed is not null)
        {
            return await _settings.GetByIdAsync(listed.Id, ct) ?? listed;
        }

        var created = ReactionWorkupSettings.CreateDefault();
        created.EffectiveUtc = Clock.UtcNow;
        await _settings.AddAsync(created, ct);
        await UnitOfWork.SaveChangesAsync(ct);
        return created;
    }

    private async Task<ReactionWorkupSettingsDto> ToDtoAsync(ReactionWorkupSettings entity, CancellationToken ct)
    {
        var groups = (await _groupers.ListAsync(g => g.IsActive && !g.IsDraft, ct))
            .OrderBy(g => g.Code, StringComparer.OrdinalIgnoreCase)
            .Select(g => new ReactionWorkupGroupOptionDto(
                g.Code, g.Name, TestGrouperMembers.Parse(g.MemberTestsJson).Select(m => m.TestCode).ToList()))
            .ToList();
        return new ReactionWorkupSettingsDto(
            entity.Id, entity.PreTransfusionTestGroupCode, entity.PostTransfusionTestGroupCode, entity.Version, groups);
    }

    /// <summary>The active, released group with this code; otherwise the newest row, so the validator can say why.</summary>
    private async Task<TestGrouper?> FindReleasedGroupAsync(string? code, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        var rows = await _groupers.ListAsync(g => g.Code == code, ct);
        return rows.FirstOrDefault(g => g.IsActive && !g.IsDraft) ?? rows.OrderByDescending(g => g.Version).FirstOrDefault();
    }

    private static string? NormalizeCode(string? code) =>
        string.IsNullOrWhiteSpace(code) ? null : code.Trim().ToUpperInvariant();
}
