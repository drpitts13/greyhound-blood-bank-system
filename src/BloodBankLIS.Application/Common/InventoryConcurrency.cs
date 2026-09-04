namespace BloodBankLIS.Application.Common;

/// <summary>
/// Maps unique-constraint and optimistic-concurrency failures on assign/issue
/// to a fail-closed user message. Two users must never both receive the same unit.
/// </summary>
public static class InventoryConcurrency
{
    public const string AllocationConflictCode = "ALLOC-CONCURRENT";
    public const string IssueConflictCode = "ISS-CONCURRENT";

    public static EvaluationResult<T>? AsFailure<T>(Exception exception)
    {
        if (TryExplain(exception, out _, out var message))
        {
            return EvaluationResult<T>.Fail(message);
        }

        return null;
    }

    public static bool TryExplain(Exception exception, out string code, out string message)
    {
        ArgumentNullException.ThrowIfNull(exception);
        var text = exception.ToString();

        if (Contains(text, "IX_Allocations_OneReservedPerUnit")
            || Contains(text, "UNIQUE constraint failed: Allocations.BloodProductId"))
        {
            code = AllocationConflictCode;
            message = $"{AllocationConflictCode}: Another user reserved this unit. Refresh and select a different unit.";
            return true;
        }

        if (Contains(text, "IX_Issues_OneOpenIssuePerUnit")
            || Contains(text, "UNIQUE constraint failed: Issues.BloodProductId"))
        {
            code = IssueConflictCode;
            message = $"{IssueConflictCode}: Another user already issued this unit. Refresh inventory.";
            return true;
        }

        if (Contains(text, "DbUpdateConcurrencyException"))
        {
            code = IssueConflictCode;
            message = $"{IssueConflictCode}: The unit changed while this action was in progress. Refresh and retry.";
            return true;
        }

        code = string.Empty;
        message = string.Empty;
        return false;
    }

    private static bool Contains(string text, string token) =>
        text.Contains(token, StringComparison.OrdinalIgnoreCase);
}
