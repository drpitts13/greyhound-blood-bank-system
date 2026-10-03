using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Application.Compliance;

/// <summary>
/// Which reaction investigations still need bench attention. Closed cases stay on the
/// investigation record and are not a shift queue.
/// </summary>
public static class OpenReactionInvestigationRule
{
    public static bool NeedsAttention(ReactionInvestigationStatus status) =>
        status is ReactionInvestigationStatus.Open or ReactionInvestigationStatus.UnderReview;

    public static int Count(IEnumerable<ReactionInvestigationStatus> statuses) =>
        statuses.Count(NeedsAttention);

    /// <summary>
    /// Fatality cases that still need CBER notification or the written report. A closed
    /// workup can still be counted, because closing it does not clear the follow-up.
    /// </summary>
    public static int CountFatalityFollowUpDue(IEnumerable<ReactionInvestigationDto> investigations) =>
        investigations.Count(static r => r.FatalityFollowUpDue);
}
