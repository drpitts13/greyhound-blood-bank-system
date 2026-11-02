using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Application.Issuing;

/// <summary>
/// How many reservations on the ready-to-issue list need a new crossmatch because
/// their only compatible crossmatch has expired. The count does not change the reservation.
/// </summary>
public static class ReadyToIssueAttentionRule
{
    public static int CountCrossmatchExpired(IEnumerable<ProductAllocationDisplayStatus> statuses) =>
        statuses.Count(static status => status == ProductAllocationDisplayStatus.CrossmatchExpired);
}
