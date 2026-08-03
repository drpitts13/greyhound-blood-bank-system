using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Isbt128;

/// <summary>
/// Extensible registry of ISBT 128 data-structure identifiers.
/// Longest-prefix match so "=%", "=<", "=>", "=*", "&gt;", "&amp;*" win over bare "=".
/// ICCBBA_VALIDATION_REQUIRED: confirm identifier set against current ICCBBA documentation.
/// </summary>
public static class IsbtDataStructureRegistry
{
    private static readonly (string Prefix, IsbtDataStructureKind Kind)[] Entries =
    [
        ("=%", IsbtDataStructureKind.AboRhd),
        ("=<", IsbtDataStructureKind.ProductCode),
        ("=>", IsbtDataStructureKind.ExpirationDate),
        ("=*", IsbtDataStructureKind.CollectionDate),
        ("&>", IsbtDataStructureKind.ExpirationDateTime),
        ("&*", IsbtDataStructureKind.CollectionDateTime),
        ("=", IsbtDataStructureKind.DonationIdentificationNumber)
    ];

    public static IReadOnlyList<(string Prefix, IsbtDataStructureKind Kind)> All => Entries;

    public static IsbtDataStructureKind Classify(string sanitized)
    {
        if (string.IsNullOrEmpty(sanitized))
            return IsbtDataStructureKind.Unknown;

        foreach (var (prefix, kind) in Entries.OrderByDescending(e => e.Prefix.Length))
        {
            if (sanitized.StartsWith(prefix, StringComparison.Ordinal))
                return kind;
        }

        return IsbtDataStructureKind.Unknown;
    }

    public static bool StartsWithSupportedIdentifier(string sanitized) =>
        Classify(sanitized) != IsbtDataStructureKind.Unknown;

    public static string? GetPrefix(IsbtDataStructureKind kind) =>
        Entries.FirstOrDefault(e => e.Kind == kind).Prefix;
}
