using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Common;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Application.Compliance;

public sealed record DowntimeEpisodeDto(
    long Id,
    DateTime StartedUtc,
    DateTime? EndedUtc,
    string Description,
    string RecordedBy,
    string? EndedBy,
    // Paper issues back-entered with an issue time inside this outage.
    int BackEntries,
    // Of these, the transfusion (or return) is still to be entered.
    int BackEntriesOpen,
    // The late-entry reason a back-entry inside this outage carries.
    string BackEntryReason);

public sealed record RecordDowntimeEpisodeRequest(DateTime StartedUtc, DateTime? EndedUtc, string Description);

public sealed record EndDowntimeEpisodeRequest(DateTime EndedUtc);

/// <summary>
/// Recorded outages (Cycle 662): the window the blood bank worked on paper, so that recovery does not depend on anyone
/// remembering it. The people who back-enter paper issues record and end them; each change is audited.
/// </summary>
public sealed class DowntimeEpisodeService
{
    public const int ListLimit = 20;

    private readonly IRepository<DowntimeEpisode> _episodes;
    private readonly IRepository<Issue> _issues;
    private readonly IAuditWriter _audit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly ICurrentUser _currentUser;

    public DowntimeEpisodeService(
        IRepository<DowntimeEpisode> episodes,
        IRepository<Issue> issues,
        IAuditWriter audit,
        IUnitOfWork unitOfWork,
        IClock clock,
        ICurrentUser currentUser)
    {
        _episodes = episodes;
        _issues = issues;
        _audit = audit;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _currentUser = currentUser;
    }

    /// <summary>The most recent outages, newest first, each with the paper issues back-entered inside it.</summary>
    public async Task<IReadOnlyList<DowntimeEpisodeDto>> ListAsync(CancellationToken ct = default)
    {
        var episodes = (await _episodes.ListAsync(ct)).OrderByDescending(e => e.StartedUtc).Take(ListLimit).ToList();
        if (episodes.Count == 0)
        {
            return [];
        }

        var earliest = episodes.Min(e => e.StartedUtc);
        var backEntries = await _issues.ListAsync(i => i.DowntimeBackEntry && i.IssuedUtc >= earliest, ct);
        return episodes.Select(e =>
        {
            var inside = backEntries.Where(i => DowntimeEpisodeRule.Covering([e], i.IssuedUtc) is not null).ToList();
            return Map(e, inside.Count, inside.Count(i => i.Status == IssueStatus.Issued));
        }).ToList();
    }

    public async Task<EvaluationResult<DowntimeEpisodeDto>> RecordAsync(RecordDowntimeEpisodeRequest request, CancellationToken ct = default)
    {
        var started = ToUtc(request.StartedUtc);
        var ended = request.EndedUtc is { } e ? ToUtc(e) : (DateTime?)null;
        var check = DowntimeEpisodeRule.Validate(started, ended, request.Description, _clock.UtcNow, await _episodes.ListAsync(ct));
        if (check.Count > 0)
        {
            return EvaluationResult<DowntimeEpisodeDto>.Blocked(new RuleEvaluation(check));
        }

        var episode = new DowntimeEpisode
        {
            StartedUtc = started,
            EndedUtc = ended,
            Description = request.Description.Trim(),
            EndedBy = ended is null ? null : _currentUser.UserName,
            CreatedUtc = _clock.UtcNow,
            CreatedBy = _currentUser.UserName
        };
        await _episodes.AddAsync(episode, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        _audit.Record(AuditEventType.Create, nameof(DowntimeEpisode), episode.Id,
            newValue: new { episode.StartedUtc, episode.EndedUtc, episode.Description });
        await _unitOfWork.SaveChangesAsync(ct);
        return EvaluationResult<DowntimeEpisodeDto>.Ok(Map(episode, 0, 0));
    }

    /// <summary>Ends an open outage at the time given (the system is back), checked against the others.</summary>
    public async Task<EvaluationResult<DowntimeEpisodeDto>> EndAsync(long id, EndDowntimeEpisodeRequest request, CancellationToken ct = default)
    {
        var episode = await _episodes.GetByIdAsync(id, ct);
        if (episode is null)
        {
            return EvaluationResult<DowntimeEpisodeDto>.Fail("Outage not found.");
        }

        if (episode.EndedUtc is not null)
        {
            return EvaluationResult<DowntimeEpisodeDto>.Fail($"Outage #{id} has already ended.");
        }

        var ended = ToUtc(request.EndedUtc);
        var others = (await _episodes.ListAsync(o => o.Id != id, ct));
        var check = DowntimeEpisodeRule.Validate(episode.StartedUtc, ended, episode.Description, _clock.UtcNow, others);
        if (check.Count > 0)
        {
            return EvaluationResult<DowntimeEpisodeDto>.Blocked(new RuleEvaluation(check));
        }

        episode.EndedUtc = ended;
        episode.EndedBy = _currentUser.UserName;
        episode.ModifiedUtc = _clock.UtcNow;
        episode.ModifiedBy = _currentUser.UserName;
        _audit.Record(AuditEventType.Update, nameof(DowntimeEpisode), episode.Id,
            oldValue: new { EndedUtc = (DateTime?)null }, newValue: new { episode.EndedUtc }, reason: "Outage ended");
        await _unitOfWork.SaveChangesAsync(ct);
        return EvaluationResult<DowntimeEpisodeDto>.Ok(Map(episode, 0, 0));
    }

    private static DateTime ToUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Local => value.ToUniversalTime(),
        DateTimeKind.Unspecified => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        _ => value
    };

    private static DowntimeEpisodeDto Map(DowntimeEpisode e, int backEntries, int open) => new(
        e.Id, e.StartedUtc, e.EndedUtc, e.Description, e.CreatedBy, e.EndedBy, backEntries, open,
        DowntimeEpisodeRule.BackEntryReason(e));
}
