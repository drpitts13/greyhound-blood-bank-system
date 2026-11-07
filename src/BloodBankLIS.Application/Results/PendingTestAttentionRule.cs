namespace BloodBankLIS.Application.Results;

/// <summary>
/// How many pending worklist rows must be verified before their specimen expires
/// (<see cref="TestWorkItemDto.VerificationDeadlineNotice"/>). The count changes nothing.
/// </summary>
public static class PendingTestAttentionRule
{
    public static int CountVerifyBeforeExpiry(IEnumerable<TestWorkItemDto> items) =>
        items.Count(static item => item.VerificationDeadlineNotice is not null);
}
