using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Common;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Application.Compliance;

public sealed record AlertDto(
    long Id,
    string Source,
    AlertSeverity Severity,
    string Title,
    string Message,
    string? Link,
    DateTime RaisedUtc,
    string? AcknowledgedBy,
    DateTime? AcknowledgedUtc,
    string? AcknowledgeNote,
    DateTime? ResolvedUtc)
{
    public static AlertDto From(Alert a) => new(
        a.Id, a.Source, a.Severity, a.Title, a.Message, a.Link, a.RaisedUtc,
        a.AcknowledgedBy, a.AcknowledgedUtc, a.AcknowledgeNote, a.ResolvedUtc);
}

public sealed record AcknowledgeAlertRequest(string? Note = null);

/// <summary>Stores, reconciles, and acknowledges in-app alerts.</summary>
public sealed class AlertService
{
    private readonly IRepository<Alert> _alerts;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditWriter _audit;

    public AlertService(
        IRepository<Alert> alerts,
        IUnitOfWork unitOfWork,
        IClock clock,
        ICurrentUser currentUser,
        IAuditWriter audit)
    {
        _alerts = alerts;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _currentUser = currentUser;
        _audit = audit;
    }

    /// <summary>Raises an event alert unless one with the same key is still active.</summary>
    public async Task<bool> RaiseAsync(AlertCandidate candidate, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        if (await _alerts.AnyAsync(a => a.Key == candidate.Key && a.ResolvedUtc == null, ct))
        {
            return false;
        }

        await _alerts.AddAsync(ToEntity(candidate), ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>
    /// Brings a condition source in step with what it found now: new keys are raised,
    /// and active alerts whose condition cleared are resolved.
    /// </summary>
    public async Task<(int Raised, int Resolved)> SyncAsync(
        string source,
        IReadOnlyCollection<AlertCandidate> found,
        CancellationToken ct = default)
    {
        var active = (await _alerts.ListAsync(a => a.Source == source && a.ResolvedUtc == null, ct)).ToList();
        var (toRaise, toResolve) = AlertRule.Reconcile(active, found.Where(c => c.Source == source).ToList());
        foreach (var candidate in toRaise)
        {
            await _alerts.AddAsync(ToEntity(candidate), ct);
        }

        // A condition that still holds but changed (a shortage now on order, say) keeps its alert and says what is true now.
        var changed = AlertRule.Changed(active, found.Where(c => c.Source == source).ToList());
        foreach (var (alert, current) in changed)
        {
            var tracked = await _alerts.GetByIdAsync(alert.Id, ct);
            if (tracked is null)
            {
                continue;
            }

            tracked.Severity = current.Severity;
            tracked.Title = current.Title;
            tracked.Message = current.Message;
            tracked.Link = current.Link;
            _alerts.Update(tracked);
        }

        var now = _clock.UtcNow;
        foreach (var cleared in toResolve)
        {
            var tracked = await _alerts.GetByIdAsync(cleared.Id, ct);
            if (tracked is null)
            {
                continue;
            }

            tracked.ResolvedUtc = now;
            _alerts.Update(tracked);
        }

        if (toRaise.Count > 0 || toResolve.Count > 0 || changed.Count > 0)
        {
            await _unitOfWork.SaveChangesAsync(ct);
        }

        return (toRaise.Count, toResolve.Count);
    }

    public async Task<IReadOnlyList<AlertDto>> ListAsync(bool openOnly, CancellationToken ct = default)
    {
        var since = _clock.UtcNow.AddDays(-7);
        var rows = openOnly
            ? await _alerts.ListAsync(a => a.ResolvedUtc == null && a.AcknowledgedUtc == null, ct)
            : await _alerts.ListAsync(a => (a.ResolvedUtc == null && a.AcknowledgedUtc == null) || a.RaisedUtc >= since, ct);
        return rows
            .OrderBy(a => AlertRule.IsOpen(a) ? 0 : 1)
            .ThenByDescending(a => a.Severity)
            .ThenByDescending(a => a.RaisedUtc)
            .ThenByDescending(a => a.Id)
            .Select(AlertDto.From)
            .ToList();
    }

    public async Task<int> CountOpenAsync(CancellationToken ct = default) =>
        (await _alerts.ListAsync(a => a.ResolvedUtc == null && a.AcknowledgedUtc == null, ct)).Count;

    public async Task<OperationResult<AlertDto>> AcknowledgeAsync(long id, AcknowledgeAlertRequest request, CancellationToken ct = default)
    {
        var alert = await _alerts.GetByIdAsync(id, ct);
        if (alert is null)
        {
            return OperationResult<AlertDto>.Fail("Alert not found.");
        }

        if (alert.AcknowledgedUtc is not null)
        {
            return OperationResult<AlertDto>.Fail($"Alert was already acknowledged by {alert.AcknowledgedBy}.");
        }

        alert.AcknowledgedBy = _currentUser.UserName;
        alert.AcknowledgedUtc = _clock.UtcNow;
        alert.AcknowledgeNote = string.IsNullOrWhiteSpace(request?.Note) ? null : request.Note.Trim();
        _alerts.Update(alert);
        _audit.Record(
            AuditEventType.Update,
            nameof(Alert),
            alert.Id,
            new { Acknowledged = false },
            new { alert.Source, alert.Title, alert.AcknowledgedBy, alert.AcknowledgeNote },
            "Alert acknowledged.");
        await _unitOfWork.SaveChangesAsync(ct);
        return OperationResult<AlertDto>.Ok(AlertDto.From(alert));
    }

    private Alert ToEntity(AlertCandidate c) => new()
    {
        Source = c.Source,
        Key = c.Key,
        Severity = c.Severity,
        Title = c.Title,
        Message = c.Message,
        Link = c.Link,
        RaisedUtc = _clock.UtcNow
    };
}
