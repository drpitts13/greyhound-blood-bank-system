namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Phrase for a bench modification on the unit timeline.
/// The modification record does not store a patient. When no product order points
/// at it, the row says so and does not invent a patient. A blank reason is omitted.
/// </summary>
public static class BenchModificationActivityCue
{
    public const string NoPatientOrder = "No patient order was recorded.";

    public static string Format(string? modificationType, string? role, string? reason, bool patientOrderRecorded)
    {
        var type = string.IsNullOrWhiteSpace(modificationType) ? "Modification" : modificationType.Trim();
        var roleText = string.IsNullOrWhiteSpace(role) ? null : role.Trim();
        var head = roleText is null ? type : $"{type} ({roleText})";
        var reasonText = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        var body = reasonText is null ? head : $"{head} — {reasonText}";
        return patientOrderRecorded ? body : $"{body}. {NoPatientOrder}";
    }
}
