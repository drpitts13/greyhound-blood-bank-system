namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Phrase for a bench modification on the unit timeline.
/// The modification record does not store a patient. When no product order points
/// at it, the row says so. When one does, the row names that patient. A missing
/// order does not invent a patient. Disagreeing orders do not pick one. A blank
/// reason is omitted.
/// </summary>
public static class BenchModificationActivityCue
{
    public const string NoPatientOrder = "No patient order was recorded.";

    public static string Format(
        string? modificationType,
        string? role,
        string? reason,
        string? patientLabel = null,
        bool orderCouldNotBeRead = false,
        bool linkedOrdersDisagree = false)
    {
        var type = string.IsNullOrWhiteSpace(modificationType) ? "Modification" : modificationType.Trim();
        var roleText = string.IsNullOrWhiteSpace(role) ? null : role.Trim();
        var head = roleText is null ? type : $"{type} ({roleText})";
        var reasonText = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        var body = reasonText is null ? head : $"{head} — {reasonText}";
        if (linkedOrdersDisagree)
        {
            return $"{body}. The linked orders do not name one patient.";
        }

        if (!string.IsNullOrWhiteSpace(patientLabel))
        {
            return $"{body} for {patientLabel.Trim()}";
        }

        return orderCouldNotBeRead
            ? $"{body}. The order could not be read."
            : $"{body}. {NoPatientOrder}";
    }
}
