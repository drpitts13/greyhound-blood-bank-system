using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Common;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Application.Compliance;

public sealed record ReviewOverrideRequest(OverrideReviewOutcome Outcome, string? Note);

public sealed record OverrideReviewItemDto(
    long Id,
    OverrideAction Action,
    string ContextType,
    long ContextId,
    string RuleCode,
    string Reason,
    string AuthorizedBy,
    string PerformedBy,
    DateTime OverriddenUtc,
    string? ContextHref,
    OverrideReviewOutcome? ReviewOutcome = null,
    string? ReviewedBy = null,
    DateTime? ReviewedUtc = null,
    string? ReviewNote = null,
    AuthorizerDirectoryMatch? AuthorizerMatch = null,
    string? AuthorizerReference = null)
{
    public static OverrideReviewItemDto From(Override o) => new(
        o.Id, o.Action, o.ContextType, o.ContextId, o.RuleCode, o.Reason, o.AuthorizedBy, o.CreatedBy, o.OverriddenUtc,
        // Issue overrides carry the unit id; the unit page shows its issue and status history.
        o.ContextType == nameof(Issue) ? $"/inventory/units/{o.ContextId}" : null,
        o.ReviewOutcome, o.ReviewedBy, o.ReviewedUtc, o.ReviewNote, o.AuthorizerMatch, o.AuthorizerReference);
}

/// <summary>
/// Second-person review of overrides after the fact (<see cref="PermissionCodes.OverrideApprove"/>). Lists
/// unreviewed overrides oldest first and records each review, audited. Never changes the overridden action.
/// </summary>
public sealed class OverrideReviewService
{
    private readonly IRepository<Override> _overrides;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditWriter _audit;
    private readonly IPermissionEvaluator? _permissions;

    public OverrideReviewService(
        IRepository<Override> overrides,
        IUnitOfWork unitOfWork,
        IClock clock,
        ICurrentUser currentUser,
        IAuditWriter audit,
        IPermissionEvaluator? permissions = null)
    {
        _overrides = overrides;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _currentUser = currentUser;
        _audit = audit;
        _permissions = permissions;
    }

    public async Task<IReadOnlyList<OverrideReviewItemDto>> ListPendingAsync(CancellationToken ct = default) =>
        (await _overrides.ListAsync(o => o.ReviewedUtc == null, ct))
            .OrderBy(o => o.OverriddenUtc)
            .Select(OverrideReviewItemDto.From)
            .ToList();

    public async Task<EvaluationResult<OverrideReviewItemDto>> ReviewAsync(long id, ReviewOverrideRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (_permissions is not null
            && !await _permissions.HasPermissionAsync(_currentUser.UserName, PermissionCodes.OverrideApprove, ct))
        {
            return EvaluationResult<OverrideReviewItemDto>.Blocked(new RuleEvaluation([
                RuleResult.HardStop(OverrideReviewRule.Code, "Reviewing overrides requires the override.approve permission.")]));
        }

        var row = await _overrides.GetByIdAsync(id, ct);
        if (row is null)
        {
            return EvaluationResult<OverrideReviewItemDto>.Fail("Override not found.");
        }

        var check = OverrideReviewRule.Evaluate(
            row.ReviewedUtc is not null, _currentUser.UserName, row.CreatedBy, row.AuthorizedBy, request.Outcome, request.Note);
        if (check.Severity == RuleSeverity.HardStop)
        {
            return EvaluationResult<OverrideReviewItemDto>.Blocked(new RuleEvaluation([check]));
        }

        row.ReviewOutcome = request.Outcome;
        row.ReviewNote = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        row.ReviewedBy = _currentUser.UserName;
        row.ReviewedUtc = _clock.UtcNow;
        _overrides.Update(row);
        _audit.Record(
            AuditEventType.Override,
            nameof(Override),
            row.Id,
            oldValue: new { Reviewed = false },
            newValue: new { Action = "Reviewed", row.ReviewOutcome, row.ReviewedBy, row.ReviewedUtc, row.RuleCode, row.ContextType, row.ContextId },
            reason: row.ReviewNote ?? row.ReviewOutcome.ToString());
        await _unitOfWork.SaveChangesAsync(ct);
        return EvaluationResult<OverrideReviewItemDto>.Ok(OverrideReviewItemDto.From(row));
    }
}
