namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Where an issued unit went, for chasing its final disposition (end-user testing pass 134: the overdue final
/// disposition board gave the unit, patient and times but not where the unit was sent, in which cooler, to whom, or
/// whether the ward received it, so the bench could not tell whom to call without opening each issue).
/// "ICU 4 · Cooler M1 · to RN Demo · received on the ward 2026-11-06 08:57 by RN Demo".
/// </summary>
public static class IssuedUnitWhereabouts
{
    public static string Text(string? location, string? cooler, string? issuedTo, DateTime? wardReceivedUtc, string? wardReceivedBy)
    {
        var parts = new List<string>
        {
            string.IsNullOrWhiteSpace(location) ? "Location not recorded" : location.Trim()
        };
        if (!string.IsNullOrWhiteSpace(cooler))
        {
            parts.Add(cooler.Trim());
        }

        if (!string.IsNullOrWhiteSpace(issuedTo))
        {
            parts.Add($"to {issuedTo.Trim()}");
        }

        parts.Add(wardReceivedUtc is DateTime received
            ? $"received on the ward {ClinicalTimeText.Local(received)}" + (string.IsNullOrWhiteSpace(wardReceivedBy) ? string.Empty : $" by {wardReceivedBy.Trim()}")
            : "not received on the ward");
        return string.Join(" · ", parts);
    }
}
