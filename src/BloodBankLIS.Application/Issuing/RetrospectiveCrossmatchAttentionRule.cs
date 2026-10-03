namespace BloodBankLIS.Application.Issuing;

/// <summary>
/// How many retrospective crossmatch work items are already past their stored due time.
/// The count does not change retrospective completion.
/// </summary>
public static class RetrospectiveCrossmatchAttentionRule
{
    public static int CountOverdue(IEnumerable<RetrospectiveCrossmatchWorkItemDto> items) =>
        items.Count(static item => item.IsOverdue);
}
