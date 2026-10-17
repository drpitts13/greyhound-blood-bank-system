namespace BloodBankLIS.Domain.Rules;

public static class FinalDispositionDueRule
{
    public const int DefaultDueHours = 24;

    public static bool IsOverdue(
        DateTime issuedUtc,
        DateTime nowUtc,
        int dueHours,
        bool hasTransfusionDocument,
        bool hasReturn,
        bool discarded)
    {
        if (hasTransfusionDocument || hasReturn || discarded)
        {
            return false;
        }

        var hours = dueHours > 0 ? dueHours : DefaultDueHours;
        return issuedUtc.AddHours(hours) < nowUtc;
    }
}
