namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// OCD-004 readiness: how far the facility is from running on a licensed ICCBBA catalog. Read-only; it never
/// changes a code. A code is Licensed when its catalog row came from a licensed import, Placeholder when it is a
/// seeded placeholder or public-subset row, and Unknown when no catalog row matches it.
/// </summary>
public static class IsbtReadinessRule
{
    public enum CodeState
    {
        Licensed = 0,
        Placeholder = 1,
        Unknown = 2,
        None = 3
    }

    /// <param name="catalog">Code (case-insensitive) to whether its catalog row is a placeholder.</param>
    public static CodeState Classify(string? code, IReadOnlyDictionary<string, bool> catalog)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return CodeState.None;
        }

        return catalog.TryGetValue(code.Trim(), out var placeholder)
            ? placeholder ? CodeState.Placeholder : CodeState.Licensed
            : CodeState.Unknown;
    }

    /// <summary>The 5-character product description code, from the stored code or the product code data.</summary>
    public static string? ProductDescriptionCode(string? productDescriptionCode, string? productCodeData)
    {
        if (!string.IsNullOrWhiteSpace(productDescriptionCode))
        {
            return productDescriptionCode.Trim();
        }

        var data = productCodeData?.Trim();
        return string.IsNullOrEmpty(data) ? null : data.Length <= 5 ? data : data[..5];
    }

    /// <summary>Ready only when a licensed version is loaded and nothing in use relies on a placeholder or unknown code.</summary>
    public static bool IsReady(int licensedVersionsLoaded, int placeholderInUse, int unknownInUse) =>
        licensedVersionsLoaded > 0 && placeholderInUse == 0 && unknownInUse == 0;

    public static string Summary(int licensedVersionsLoaded, int placeholderInUse, int unknownInUse)
    {
        if (IsReady(licensedVersionsLoaded, placeholderInUse, unknownInUse))
        {
            return "Ready: every ISBT code in use comes from a licensed ICCBBA import.";
        }

        var parts = new List<string>();
        if (licensedVersionsLoaded == 0)
        {
            parts.Add("no licensed ICCBBA extract has been imported");
        }

        if (placeholderInUse > 0)
        {
            parts.Add($"{placeholderInUse} code{(placeholderInUse == 1 ? "" : "s")} in use rel{(placeholderInUse == 1 ? "ies" : "y")} on placeholder rows");
        }

        if (unknownInUse > 0)
        {
            parts.Add($"{unknownInUse} code{(unknownInUse == 1 ? " is" : "s are")} not in the catalog at all");
        }

        return "Not ready: " + string.Join("; ", parts) + ". REQUIRES ICCBBA LICENSE (OCD-004).";
    }
}
