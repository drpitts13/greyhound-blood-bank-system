namespace BloodBankLIS.Application.Issuing;

/// <summary>
/// How many in-transit issues are past the arrival window already stored on the work item.
/// The count does not change ward receipt.
/// </summary>
public static class InTransitAttentionRule
{
    public static int CountOverdue(IEnumerable<bool> overdueFlags) =>
        overdueFlags.Count(static overdue => overdue);
}
