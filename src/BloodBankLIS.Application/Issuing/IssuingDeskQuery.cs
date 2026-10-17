namespace BloodBankLIS.Application.Issuing;

/// <summary>
/// Opens the issue desk named on the dashboard link when the signed-in user
/// can already see that board. A patient or unit prefill still opens Issue.
/// An unknown or hidden desk leaves the current board alone.
/// </summary>
public static class IssuingDeskQuery
{
    public static string Resolve(
        string? requested,
        bool canIssue,
        bool canSeeIssued,
        bool hasPatientOrUnit,
        string current = "ready")
    {
        if (hasPatientOrUnit)
        {
            return "issue";
        }

        if (string.IsNullOrWhiteSpace(requested))
        {
            return current;
        }

        var desk = requested.Trim().ToLowerInvariant();
        return desk switch
        {
            "ready" or "second" or "retro" or "transit" => canIssue ? desk : current,
            "issued" or "disposition" => canSeeIssued ? desk : current,
            "issue" or "receipt" or "return" or "transfuse" => desk,
            _ => current
        };
    }
}
