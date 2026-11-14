namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// An external identifier another chart already held is kept with its authority marked "also on" that chart's MRN
/// ("EPI PI, also on MRN-1"). These helpers read that mark back: the authority without it, and the MRN it names.
/// </summary>
public static class ExternalIdentifierAuthority
{
    private const string Marker = "also on ";

    public static string AlsoOn(string? authority, string otherMrn) =>
        string.IsNullOrWhiteSpace(authority) ? $"{Marker}{otherMrn}" : $"{authority.Trim()}, {Marker}{otherMrn}";

    /// <summary>The issuing authority without an "also on" mark; null when none.</summary>
    public static string? Base(string? authority)
    {
        if (string.IsNullOrWhiteSpace(authority))
        {
            return null;
        }

        var text = authority.Trim();
        var at = text.IndexOf(Marker, StringComparison.OrdinalIgnoreCase);
        if (at < 0)
        {
            return text;
        }

        var head = text[..at].TrimEnd().TrimEnd(',').TrimEnd();
        return head.Length == 0 ? null : head;
    }

    /// <summary>The MRN named by an "also on" mark, or null.</summary>
    public static string? OtherMrn(string? authority)
    {
        if (string.IsNullOrWhiteSpace(authority))
        {
            return null;
        }

        var at = authority.IndexOf(Marker, StringComparison.OrdinalIgnoreCase);
        if (at < 0)
        {
            return null;
        }

        var mrn = authority[(at + Marker.Length)..].Trim();
        return mrn.Length == 0 ? null : mrn;
    }

    /// <summary>Same issuer, ignoring an "also on" mark (case-insensitive).</summary>
    public static bool SameIssuer(string? first, string? second) =>
        string.Equals(Base(first), Base(second), StringComparison.OrdinalIgnoreCase);
}
