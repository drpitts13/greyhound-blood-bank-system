using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Common;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Application.Compliance;

public sealed record DowntimeReconciliationSnapshot(
    DateTime GeneratedUtc,
    int UnresolvedInterfaceErrors,
    int PendingOutboundHl7,
    int OpenIssues,
    int PendingRetrospectiveCrossmatches,
    int HashedAuditRows,
    int UnhashedAuditRows,
    string AuditChainCode,
    string AuditChainMessage);

/// <summary>
/// Aggregates existing queues for post-downtime review. Does not import paper
/// records, invent a facility SOP, or purge data.
/// </summary>
public sealed class DowntimeReconciliationService
{
    private readonly IRepository<InterfaceErrorQueueItem> _errors;
    private readonly IRepository<Hl7MessageLog> _hl7;
    private readonly IRepository<Issue> _issues;
    private readonly IAuditQuery _audit;
    private readonly IClock _clock;
    private readonly ICurrentUser _currentUser;
    private readonly IPermissionEvaluator? _permissions;

    public DowntimeReconciliationService(
        IRepository<InterfaceErrorQueueItem> errors,
        IRepository<Hl7MessageLog> hl7,
        IRepository<Issue> issues,
        IAuditQuery audit,
        IClock clock,
        ICurrentUser currentUser,
        IPermissionEvaluator? permissions = null)
    {
        _errors = errors;
        _hl7 = hl7;
        _issues = issues;
        _audit = audit;
        _clock = clock;
        _currentUser = currentUser;
        _permissions = permissions;
    }

    public async Task<EvaluationResult<DowntimeReconciliationSnapshot>> GetSnapshotAsync(
        CancellationToken ct = default)
    {
        if (_permissions is not null)
        {
            var allowed = await _permissions.HasPermissionAsync(
                _currentUser.UserName, PermissionCodes.AuditRead, ct);
            var auth = DowntimeReconciliationAuthorizationRule.Evaluate(allowed);
            if (auth.Severity == RuleSeverity.HardStop)
            {
                return EvaluationResult<DowntimeReconciliationSnapshot>.Blocked(new RuleEvaluation([auth]));
            }
        }

        var errors = await _errors.ListAsync(e => !e.Resolved, ct);
        var outbound = await _hl7.ListAsync(
            m => m.Direction == Hl7Direction.Outbound
                && (m.Status == Hl7MessageStatus.Received
                    || m.Status == Hl7MessageStatus.Errored
                    || m.Status == Hl7MessageStatus.Nacked),
            ct);
        var openIssues = await _issues.ListAsync(i => i.Status == IssueStatus.Issued, ct);
        var retro = await _issues.ListAsync(
            i => i.RetrospectiveCrossmatchDueUtc != null && i.RetrospectiveCrossmatchCompletedUtc == null,
            ct);
        var events = await _audit.ListAsync(ct);
        var hashed = events.Count(e => !string.IsNullOrWhiteSpace(e.RecordHash));
        var chain = AuditHashChainRule.Verify(events);

        var snapshot = new DowntimeReconciliationSnapshot(
            _clock.UtcNow,
            errors.Count,
            outbound.Count,
            openIssues.Count,
            retro.Count,
            hashed,
            events.Count - hashed,
            chain.Code,
            chain.Message);

        return EvaluationResult<DowntimeReconciliationSnapshot>.Ok(snapshot, new RuleEvaluation([chain]));
    }
}
