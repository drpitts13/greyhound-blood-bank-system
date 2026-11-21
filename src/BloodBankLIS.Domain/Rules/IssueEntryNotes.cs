namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// How an issue was entered, as a reader of the patient's product history needs to know it (Cycle 676): entered after
/// the fact, from a paper record during downtime, outside every recorded outage (with the reason given), or to an infant
/// without a crossmatch under the neonatal rules. Each was kept on the issue and in the audit; this puts them on the
/// record itself, in plain words, so a review does not have to open the audit trail to find them.
/// </summary>
public static class IssueEntryNotes
{
    public static IReadOnlyList<string> For(string? lateEntryReason, bool downtimeBackEntry, string? outsideOutageReason, bool neonatalNoCrossmatch)
    {
        var notes = new List<string>();
        if (downtimeBackEntry)
        {
            notes.Add("Entered from a paper issue record after downtime");
        }
        else if (!string.IsNullOrWhiteSpace(lateEntryReason))
        {
            notes.Add($"Late entry: {lateEntryReason.Trim()}");
        }

        if (downtimeBackEntry && !string.IsNullOrWhiteSpace(lateEntryReason))
        {
            notes.Add($"Reason: {lateEntryReason.Trim()}");
        }

        if (!string.IsNullOrWhiteSpace(outsideOutageReason))
        {
            notes.Add($"Outside every recorded outage: {outsideOutageReason.Trim()}");
        }

        if (neonatalNoCrossmatch)
        {
            notes.Add("Infant, negative screen: group O issued without a crossmatch (neonatal rules)");
        }

        return notes;
    }
}
