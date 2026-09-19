using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Common;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Application.Admin;

public sealed record CodedCommentDefinitionDto(
    long Id,
    string Code,
    string CommentText,
    CommentPage Page,
    int SortOrder,
    bool IsActive)
{
    public static CodedCommentDefinitionDto From(CodedCommentDefinition e) => new(
        e.Id, e.Code, e.CommentText, e.Page, e.SortOrder, e.IsActive);
}

public sealed record SaveCodedCommentDefinitionRequest(
    string Code,
    string CommentText,
    CommentPage Page,
    int SortOrder);

public sealed record CodedCommentRefDto(
    string Code,
    string CommentText,
    CommentPage Page,
    int SortOrder);

public sealed class CodedCommentAdminService : ConfigAdminServiceBase
{
    private readonly IRepository<CodedCommentDefinition> _comments;
    private readonly IPermissionEvaluator? _permissionEvaluator;

    public CodedCommentAdminService(
        IRepository<CodedCommentDefinition> comments,
        IUnitOfWork unitOfWork,
        IClock clock,
        ICurrentUser currentUser,
        IAuditWriter audit,
        IConfigurationHistoryWriter history,
        IPermissionEvaluator? permissionEvaluator = null)
        : base(unitOfWork, clock, currentUser, audit, history)
    {
        _comments = comments;
        _permissionEvaluator = permissionEvaluator;
    }

    public async Task<IReadOnlyList<CodedCommentDefinitionDto>> ListAsync(
        bool includeInactive,
        CommentPage? page = null,
        CancellationToken ct = default)
    {
        var list = includeInactive
            ? await _comments.ListAsync(ct)
            : await _comments.ListAsync(e => e.IsActive, ct);
        if (page is not null)
        {
            list = list.Where(e => e.Page == page).ToList();
        }

        return list
            .OrderBy(e => e.Page)
            .ThenBy(e => e.SortOrder)
            .ThenBy(e => e.Code)
            .Select(CodedCommentDefinitionDto.From)
            .ToList();
    }

    public async Task<CodedCommentDefinitionDto?> GetAsync(long id, CancellationToken ct = default)
    {
        var entity = await _comments.GetByIdAsync(id, ct);
        return entity is null ? null : CodedCommentDefinitionDto.From(entity);
    }

    public async Task<EvaluationResult<CodedCommentDefinitionDto>> CreateAsync(
        SaveCodedCommentDefinitionRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var denied = await RejectUnauthorizedAsync(
            PermissionCodes.AdminConfigEdit, CodedCommentAuthorizationRule.EvaluateCreate, ct);
        if (denied is not null)
        {
            return denied;
        }

        var code = NormalizeCode(request.Code);
        var text = request.CommentText?.Trim() ?? string.Empty;
        var duplicate = await _comments.AnyAsync(e => e.Page == request.Page && e.Code == code, ct);
        var entity = new CodedCommentDefinition
        {
            Code = code,
            CommentText = text,
            Page = request.Page,
            SortOrder = request.SortOrder,
            IsActive = true
        };

        var validation = CodedCommentDefinitionValidator.Validate(entity, duplicate);
        if (validation.IsHardStopped)
        {
            return EvaluationResult<CodedCommentDefinitionDto>.Blocked(validation);
        }

        await _comments.AddAsync(entity, ct);
        await UnitOfWork.SaveChangesAsync(ct);
        var dto = CodedCommentDefinitionDto.From(entity);
        RecordChange(
            nameof(CodedCommentDefinition), entity.Id, 1, ConfigChangeAction.Create,
            AuditEventType.Configure, null, dto, null);
        await UnitOfWork.SaveChangesAsync(ct);
        return EvaluationResult<CodedCommentDefinitionDto>.Ok(dto, validation);
    }

    public async Task<EvaluationResult<CodedCommentDefinitionDto>> UpdateAsync(
        long id,
        SaveCodedCommentDefinitionRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var denied = await RejectUnauthorizedAsync(
            PermissionCodes.AdminConfigEdit, CodedCommentAuthorizationRule.EvaluateUpdate, ct);
        if (denied is not null)
        {
            return denied;
        }

        var entity = await _comments.GetByIdAsync(id, ct);
        if (entity is null)
        {
            return EvaluationResult<CodedCommentDefinitionDto>.Fail("Coded comment not found.");
        }

        var code = NormalizeCode(request.Code);
        var text = request.CommentText?.Trim() ?? string.Empty;
        var duplicate = await _comments.AnyAsync(e => e.Page == request.Page && e.Code == code && e.Id != id, ct);
        var candidate = new CodedCommentDefinition
        {
            Code = code,
            CommentText = text,
            Page = request.Page,
            SortOrder = request.SortOrder
        };
        var validation = CodedCommentDefinitionValidator.Validate(candidate, duplicate);
        if (validation.IsHardStopped)
        {
            return EvaluationResult<CodedCommentDefinitionDto>.Blocked(validation);
        }

        var old = CodedCommentDefinitionDto.From(entity);
        entity.Code = code;
        entity.CommentText = text;
        entity.Page = request.Page;
        entity.SortOrder = request.SortOrder;
        _comments.Update(entity);

        RecordChange(
            nameof(CodedCommentDefinition), entity.Id, 1, ConfigChangeAction.Update,
            AuditEventType.Configure, old, CodedCommentDefinitionDto.From(entity), null);
        await UnitOfWork.SaveChangesAsync(ct);
        return EvaluationResult<CodedCommentDefinitionDto>.Ok(CodedCommentDefinitionDto.From(entity), validation);
    }

    public async Task<EvaluationResult<CodedCommentDefinitionDto>> SetActiveAsync(
        long id,
        bool active,
        CancellationToken ct = default)
    {
        var denied = await RejectUnauthorizedAsync(
            PermissionCodes.AdminConfigActivate,
            active
                ? CodedCommentAuthorizationRule.EvaluateActivate
                : CodedCommentAuthorizationRule.EvaluateDeactivate,
            ct);
        if (denied is not null)
        {
            return denied;
        }

        var entity = await _comments.GetByIdAsync(id, ct);
        if (entity is null)
        {
            return EvaluationResult<CodedCommentDefinitionDto>.Fail("Coded comment not found.");
        }

        var old = CodedCommentDefinitionDto.From(entity);
        entity.IsActive = active;
        _comments.Update(entity);
        var action = active ? ConfigChangeAction.Activate : ConfigChangeAction.Deactivate;
        RecordChange(
            nameof(CodedCommentDefinition), entity.Id, 1, action, ToAuditType(action),
            old, CodedCommentDefinitionDto.From(entity), null);
        await UnitOfWork.SaveChangesAsync(ct);
        return EvaluationResult<CodedCommentDefinitionDto>.Ok(CodedCommentDefinitionDto.From(entity), new RuleEvaluation([]));
    }

    private static string NormalizeCode(string? code) =>
        string.IsNullOrWhiteSpace(code) ? string.Empty : code.Trim().ToUpperInvariant();

    private async Task<EvaluationResult<CodedCommentDefinitionDto>?> RejectUnauthorizedAsync(
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
            ? EvaluationResult<CodedCommentDefinitionDto>.Blocked(new RuleEvaluation([auth]))
            : null;
    }
}
