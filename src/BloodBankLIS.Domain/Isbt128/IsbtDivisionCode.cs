namespace BloodBankLIS.Domain.Isbt128;

/// <summary>
/// ISBT 128 alphabetic division encoding used when dividing a component.
/// Collection type is preserved (default <c>V</c>). Division <c>00</c> becomes
/// <c>0A</c>/<c>0B</c>/…; a first-level code such as <c>0A</c> subdivides to
/// <c>Aa</c>/<c>Ab</c>. Second-level codes cannot be divided further without
/// an extended division structure.
/// </summary>
public static class IsbtDivisionCode
{
    public const char DefaultCollectionType = 'V';
    public const string Undivided = "00";
    public const int ProductDescriptionCodeLength = 5;
    public const int ProductCodeDataLength = 8;

    public const string LevelExceededCode = "MOD-DIVIDE-LEVEL-EXCEEDED";
    public const string DivisionExhaustedCode = "MOD-DIVIDE-DIVISION-EXHAUSTED";
    public const string ProductCodeRequiredCode = "MOD-DIVIDE-PRODUCT-CODE-REQUIRED";

    /// <summary>
    /// Allocates the next <paramref name="count"/> unused child division codes
    /// from <paramref name="sourceDivision"/>.
    /// </summary>
    public static bool TryAllocate(
        string? sourceDivision,
        int count,
        IReadOnlyCollection<string>? usedDivisions,
        out IReadOnlyList<string> allocated,
        out string? errorCode,
        out string? errorMessage)
    {
        allocated = Array.Empty<string>();
        errorCode = null;
        errorMessage = null;

        if (count < 1)
        {
            return true;
        }

        var source = NormalizeDivision(sourceDivision);
        IEnumerable<string> candidates;
        if (source == Undivided)
        {
            candidates = Enumerable.Range(0, 26).Select(i => "0" + (char)('A' + i));
        }
        else if (IsFirstLevel(source))
        {
            var parent = char.ToUpperInvariant(source[1]);
            candidates = Enumerable.Range(0, 26).Select(i => string.Concat(parent, (char)('a' + i)));
        }
        else
        {
            errorCode = LevelExceededCode;
            errorMessage =
                $"Division code '{source}' is already at the second ISBT division level and cannot be subdivided further without an extended division code.";
            return false;
        }

        var used = new HashSet<string>(usedDivisions ?? Array.Empty<string>(), StringComparer.Ordinal);
        var taken = new List<string>(count);
        foreach (var candidate in candidates)
        {
            if (used.Contains(candidate))
            {
                continue;
            }

            taken.Add(candidate);
            if (taken.Count == count)
            {
                break;
            }
        }

        if (taken.Count < count)
        {
            errorCode = DivisionExhaustedCode;
            errorMessage = "No remaining ISBT division codes are available for this donation and product.";
            return false;
        }

        allocated = taken;
        return true;
    }

    public static string BuildProductCodeData(string productDescriptionCode, string collectionType, string division)
    {
        var pdc = RequirePdc(productDescriptionCode);
        var collection = ResolveCollectionType(collectionType, productCodeData: null);
        var div = NormalizeDivision(division);
        return pdc + collection + div;
    }

    public static string? ResolveProductDescriptionCode(string? productDescriptionCode, string? productCodeData)
    {
        var pdc = (productDescriptionCode ?? string.Empty).Trim();
        if (pdc.Length >= ProductDescriptionCodeLength)
        {
            return pdc[..ProductDescriptionCodeLength];
        }

        var data = (productCodeData ?? string.Empty).Trim();
        return data.Length >= ProductDescriptionCodeLength ? data[..ProductDescriptionCodeLength] : null;
    }

    /// <summary>
    /// Uses the modification-rule target PDC when it differs from the source;
    /// otherwise keeps the source PDC so divided codes do not need catalog rows.
    /// </summary>
    public static string? ResolveBaseProductDescriptionCode(string? sourcePdc, string? targetPdc)
    {
        var source = ResolveProductDescriptionCode(sourcePdc, null);
        var target = ResolveProductDescriptionCode(targetPdc, null);
        if (target is not null && !string.Equals(source, target, StringComparison.OrdinalIgnoreCase))
        {
            return target;
        }

        return source ?? target;
    }

    public static string ResolveCollectionType(string? collectionTypeCode, string? productCodeData)
    {
        var typed = (collectionTypeCode ?? string.Empty).Trim();
        if (typed.Length >= 1)
        {
            return typed[..1];
        }

        var data = (productCodeData ?? string.Empty).Trim();
        if (data.Length >= ProductDescriptionCodeLength + 1)
        {
            return data.Substring(ProductDescriptionCodeLength, 1);
        }

        return DefaultCollectionType.ToString();
    }

    public static string ResolveDivision(string? divisionCode, string? productCodeData)
    {
        var typed = (divisionCode ?? string.Empty).Trim();
        if (typed.Length == 2)
        {
            return typed;
        }

        var data = (productCodeData ?? string.Empty).Trim();
        if (data.Length >= ProductCodeDataLength)
        {
            return data[^2..];
        }

        return Undivided;
    }

    public static string FormatCollectionAndDivision(string collectionType, string division) =>
        ResolveCollectionType(collectionType, null) + NormalizeDivision(division);

    private static string NormalizeDivision(string? division)
    {
        var value = (division ?? string.Empty).Trim();
        return value.Length == 2 ? value : Undivided;
    }

    private static bool IsFirstLevel(string division) =>
        division.Length == 2 && division[0] == '0' && char.IsAsciiLetter(division[1]);

    private static string RequirePdc(string productDescriptionCode)
    {
        var pdc = ResolveProductDescriptionCode(productDescriptionCode, null);
        if (pdc is null)
        {
            throw new ArgumentException(
                "A 5-character ISBT product description code is required.",
                nameof(productDescriptionCode));
        }

        return pdc;
    }
}
