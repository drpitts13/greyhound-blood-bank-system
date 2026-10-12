using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Common;
using BloodBankLIS.Application.Issuing;
using BloodBankLIS.Application.Results;
using BloodBankLIS.Application.Specimens;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Application.Compliance;

public sealed record CreateShiftHandoffRequest(string Notes, string? ToUser = null);

public sealed record ShiftHandoffDto(
    long Id,
    DateTime RecordedUtc,
    string FromUser,
    string? ToUser,
    string Notes,
    int RetrospectiveDue,
    int RetrospectiveOverdue,
    int OutstandingIssued,
    int InTransit,
    int InTransitOverdue,
    int PendingTests,
    int PendingSpecimens,
    int OpenReactions,
    int FatalityFollowUpDue,
    string? AcknowledgedBy,
    DateTime? AcknowledgedUtc)
{
    public bool IsAcknowledged => AcknowledgedUtc is not null;

    public static ShiftHandoffDto From(ShiftHandoff h) => new(
        h.Id, h.RecordedUtc, h.FromUser, h.ToUser, h.Notes,
        h.RetrospectiveDue, h.RetrospectiveOverdue, h.OutstandingIssued,
        h.InTransit, h.InTransitOverdue, h.PendingTests, h.PendingSpecimens,
        h.OpenReactions, h.FatalityFollowUpDue, h.AcknowledgedBy, h.AcknowledgedUtc);
}

/// <summary>
/// Records and acknowledges end-of-shift packets. Snapshot counts use the same
/// queues as the dashboard and do not change clinical gates.
/// </summary>
public sealed class ShiftHandoffService
{
    private readonly IRepository<ShiftHandoff> _handoffs;
    private readonly IssuingService? _issuing;
    private readonly TestWorklistService? _tests;
    private readonly SpecimenService? _specimens;
    private readonly ReactionInvestigationService? _reactions;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditWriter? _audit;
    private readonly IPermissionEvaluator? _permissions;

    public ShiftHandoffService(
        IRepository<ShiftHandoff> handoffs,
        IUnitOfWork unitOfWork,
        IClock clock,
        ICurrentUser currentUser,
        IssuingService? issuing = null,
        TestWorklistService? tests = null,
        SpecimenService? specimens = null,
        ReactionInvestigationService? reactions = null,
        IAuditWriter? audit = null,
        IPermissionEvaluator? permissions = null)
    {
        _handoffs = handoffs;
        _issuing = issuing;
        _tests = tests;
        _specimens = specimens;
        _reactions = reactions;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _currentUser = currentUser;
        _audit = audit;
        _permissions = permissions;
    }

    public async Task<IReadOnlyList<ShiftHandoffDto>> ListRecentAsync(int take = 20, CancellationToken ct = default)
    {
        var rows = await _handoffs.ListAsync(ct);
        return rows
            .OrderByDescending(h => h.RecordedUtc)
            .ThenByDescending(h => h.Id)
            .Take(Math.Clamp(take, 1, 100))
            .Select(ShiftHandoffDto.From)
            .ToList();
    }

    public async Task<ShiftHandoffDto?> GetLatestOpenAsync(CancellationToken ct = default)
    {
        var rows = await _handoffs.ListAsync(h => h.AcknowledgedUtc == null, ct);
        var latest = rows.OrderByDescending(h => h.RecordedUtc).ThenByDescending(h => h.Id).FirstOrDefault();
        return latest is null ? null : ShiftHandoffDto.From(latest);
    }

    public async Task<OperationResult<ShiftHandoff>> CreateAsync(
        CreateShiftHandoffRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var denied = await RejectUnauthorizedAsync(ct);
        if (denied is not null)
        {
            return denied;
        }

        var notesRule = ShiftHandoffRule.EvaluateNotes(request.Notes);
        if (notesRule.Severity == RuleSeverity.HardStop)
        {
            return OperationResult<ShiftHandoff>.Fail(notesRule.Message);
        }

        var retro = _issuing is null
            ? Array.Empty<RetrospectiveCrossmatchWorkItemDto>()
            : await _issuing.ListPendingRetrospectiveCrossmatchesAsync(ct);
        var outstanding = _issuing is null
            ? Array.Empty<OutstandingIssueWorkItemDto>()
            : await _issuing.ListOutstandingIssuedAsync(ct);
        var transit = _issuing is null
            ? Array.Empty<InTransitWorkItemDto>()
            : await _issuing.ListInTransitAsync(ct);
        var pendingTests = _tests is null
            ? Array.Empty<TestWorkItemDto>()
            : await _tests.ListPendingGlobalAsync(ct);
        var pendingSpecimens = _specimens is null
            ? Array.Empty<PendingSpecimenWorkItemDto>()
            : await _specimens.ListPendingReceiptAsync(ct);
        var reactions = _reactions is null
            ? Array.Empty<ReactionInvestigationDto>()
            : await _reactions.ListDtosAsync(ct);

        var row = new ShiftHandoff
        {
            RecordedUtc = _clock.UtcNow,
            FromUser = _currentUser.UserName,
            ToUser = string.IsNullOrWhiteSpace(request.ToUser) ? null : request.ToUser.Trim(),
            Notes = request.Notes.Trim(),
            RetrospectiveDue = retro.Count,
            RetrospectiveOverdue = RetrospectiveCrossmatchAttentionRule.CountOverdue(retro),
            OutstandingIssued = outstanding.Count,
            InTransit = transit.Count,
            InTransitOverdue = InTransitAttentionRule.CountOverdue(transit.Select(t => t.IsOverdue)),
            PendingTests = pendingTests.Count,
            PendingSpecimens = pendingSpecimens.Count,
            OpenReactions = OpenReactionInvestigationRule.Count(reactions.Select(r => r.Status)),
            FatalityFollowUpDue = OpenReactionInvestigationRule.CountFatalityFollowUpDue(reactions)
        };

        await _handoffs.AddAsync(row, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        _audit?.Record(
            AuditEventType.ShiftHandoff,
            nameof(ShiftHandoff),
            row.Id,
            newValue: Snapshot(row),
            reason: "Shift handoff recorded.");
        await _unitOfWork.SaveChangesAsync(ct);
        return OperationResult<ShiftHandoff>.Ok(row);
    }

    public async Task<OperationResult<ShiftHandoff>> AcknowledgeAsync(long id, CancellationToken ct = default)
    {
        var denied = await RejectUnauthorizedAsync(ct);
        if (denied is not null)
        {
            return denied;
        }

        var row = await _handoffs.GetByIdAsync(id, ct);
        if (row is null)
        {
            return OperationResult<ShiftHandoff>.Fail("Shift handoff not found.");
        }

        var ack = ShiftHandoffRule.EvaluateAcknowledge(row.FromUser, _currentUser.UserName, row.AcknowledgedUtc);
        if (ack.Severity == RuleSeverity.HardStop)
        {
            return OperationResult<ShiftHandoff>.Fail(ack.Message);
        }

        var previous = new { row.AcknowledgedBy, row.AcknowledgedUtc };
        row.AcknowledgedBy = _currentUser.UserName;
        row.AcknowledgedUtc = _clock.UtcNow;
        _handoffs.Update(row);
        _audit?.Record(
            AuditEventType.ShiftHandoff,
            nameof(ShiftHandoff),
            row.Id,
            oldValue: previous,
            newValue: new { row.AcknowledgedBy, row.AcknowledgedUtc },
            reason: "Shift handoff acknowledged.");
        await _unitOfWork.SaveChangesAsync(ct);
        return OperationResult<ShiftHandoff>.Ok(row);
    }

    private static object Snapshot(ShiftHandoff row) => new
    {
        row.FromUser,
        row.ToUser,
        row.Notes,
        row.RetrospectiveDue,
        row.RetrospectiveOverdue,
        row.OutstandingIssued,
        row.InTransit,
        row.InTransitOverdue,
        row.PendingTests,
        row.PendingSpecimens,
        row.OpenReactions,
        row.FatalityFollowUpDue
    };

    private async Task<OperationResult<ShiftHandoff>?> RejectUnauthorizedAsync(CancellationToken ct)
    {
        if (_permissions is null)
        {
            return null;
        }

        var allowed = await _permissions.HasPermissionAsync(
            _currentUser.UserName, PermissionCodes.AuditRead, ct);
        var auth = ShiftHandoffRule.EvaluatePermission(allowed);
        return auth.Severity == RuleSeverity.HardStop
            ? OperationResult<ShiftHandoff>.Fail(auth.Message)
            : null;
    }
}
