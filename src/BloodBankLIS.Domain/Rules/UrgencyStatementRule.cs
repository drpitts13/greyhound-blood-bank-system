namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// The requesting physician's signed statement that the clinical situation was urgent enough to release blood before
/// compatibility testing was complete (21 CFR 606.151(e); AABB emergency release). End-user testing pass 117: the
/// verbal authorizer was recorded at issue, but the signed statement that must follow was neither recorded nor chased.
/// Applies to every issue released with testing incomplete; outstanding until the blood bank records who signed it
/// and when it was received. Information and follow-up only: nothing is blocked by it.
/// </summary>
public static class UrgencyStatementRule
{
    public const int MaxSignedByLength = 200;

    public static bool Applies(bool testsIncompleteAtIssue) => testsIncompleteAtIssue;

    public const string NotNeeded = "Not needed";
    public const string NotReceived = "Not received";

    /// <summary>How the overrides report states it (pass 120): not needed, not received, or "Received from Dr. Lee".</summary>
    public static string ReportText(bool testsIncompleteAtIssue, DateTime? receivedUtc, string? signedBy) =>
        !Applies(testsIncompleteAtIssue) ? NotNeeded
        : receivedUtc is null ? NotReceived
        : $"Received from {signedBy}";

    public static bool Outstanding(bool testsIncompleteAtIssue, DateTime? receivedUtc) =>
        Applies(testsIncompleteAtIssue) && receivedUtc is null;

    /// <summary>Null when the statement can be recorded; otherwise why not.</summary>
    public static string? Validate(bool testsIncompleteAtIssue, DateTime? alreadyReceivedUtc, string? signedBy, DateTime receivedUtc, DateTime issuedUtc, DateTime nowUtc)
    {
        if (!Applies(testsIncompleteAtIssue))
        {
            return "This issue was not released before testing was complete, so no physician statement is needed.";
        }

        if (alreadyReceivedUtc is not null)
        {
            return "The physician statement for this issue is already recorded.";
        }

        if (string.IsNullOrWhiteSpace(signedBy))
        {
            return "Enter the physician who signed the statement.";
        }

        if (signedBy.Trim().Length > MaxSignedByLength)
        {
            return $"The physician's name can be at most {MaxSignedByLength} characters.";
        }

        if (receivedUtc > nowUtc.AddMinutes(5))
        {
            return "The date received cannot be in the future.";
        }

        return receivedUtc < issuedUtc.AddDays(-1)
            ? "The statement cannot have been received before the unit was issued."
            : null;
    }
}
