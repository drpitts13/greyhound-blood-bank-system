namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// The counts a cooler check or stock count writes in its summary, leaving out the ones that are zero (end-user testing
/// pass 291: "3 of 4 packed, 1 missing, 0 issued to another cooler, 0 not issued, 0 unresolved scans" buried the one
/// unit missing among three zeros).
/// </summary>
public static class CountPartsText
{
    /// <summary>"1 missing, 2 not issued": the parts whose count is not zero, in order.</summary>
    public static string NonZero(params (int Count, string Label)[] parts) =>
        string.Join(", ", parts.Where(p => p.Count != 0).Select(p => $"{p.Count} {p.Label}"));

    /// <summary>"{head}" followed by the non-zero parts: "3 of 4 packed, 1 missing".</summary>
    public static string With(string head, params (int Count, string Label)[] parts) =>
        NonZero(parts) is { Length: > 0 } rest ? $"{head}, {rest}" : head;

    /// <summary>
    /// A summary already written with every count ("…, 0 not issued, 0 unresolved scans") read without its zero parts,
    /// so checks recorded before this change read the same way. The first part (the head) is kept as it is.
    /// </summary>
    public static string DropZeroParts(string text)
    {
        var parts = text.Split(", ");
        return parts.Length <= 1
            ? text
            : string.Join(", ", parts.Take(1).Concat(parts.Skip(1).Where(p => !p.StartsWith("0 ", StringComparison.Ordinal))));
    }
}
