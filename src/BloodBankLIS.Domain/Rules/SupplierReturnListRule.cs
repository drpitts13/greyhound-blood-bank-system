namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Cycle 686: units returned to the supplier are grouped into one return for its list. A return run gives its reason
/// once (for example a recall number), so a return is the reason (ignoring case and spaces) and the UTC day.
/// </summary>
public static class SupplierReturnListRule
{
    public static string ReturnKey(string? reason, DateTime returnedUtc)
    {
        var why = string.Join(' ', (reason ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries)).ToUpperInvariant();
        return $"{why}|{returnedUtc.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)}";
    }
}
