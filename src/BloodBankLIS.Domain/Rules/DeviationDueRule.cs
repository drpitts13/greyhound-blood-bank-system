using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// A deviation's owner and due date (end-user testing pass 98: deviations showed how long they had been open, but not who
/// was working them or by when). A deviation is overdue when it is not closed and its due date has passed; with no due
/// date it is never overdue. The due date is the quality unit's to set.
/// </summary>
public static class DeviationDueRule
{
    public const int MaxAssigneeLength = 100;

    public static bool IsOverdue(DeviationStatus status, DateOnly? dueOn, DateOnly today) =>
        status != DeviationStatus.Closed && dueOn is DateOnly due && due < today;
}
