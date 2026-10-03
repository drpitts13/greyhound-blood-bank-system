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
}
