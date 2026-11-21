namespace BloodBankLIS.Web.Services;

/// <summary>
/// What the bedside reported with a suspected reaction (what was done, and what was seen), shown on the investigation
/// beside the vital signs (end-user testing pass 289: it was only on the printed reaction report, so the technologist
/// working the case did not see "temperature rose 1.8C with rigors" or "no visible haemoglobinuria").
/// </summary>
public static class ReactionBedsideReportText
{
    public static string? For(string? actions, string? observations)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(actions))
        {
            parts.Add(actions.Trim());
        }

        if (!string.IsNullOrWhiteSpace(observations))
        {
            parts.Add($"Observations: {observations.Trim()}");
        }

        return parts.Count == 0 ? null : string.Join(" ", parts.Select(p => p.EndsWith('.') ? p : p + "."));
    }
}
