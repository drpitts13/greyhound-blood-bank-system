using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Isbt128.Parsing;

/// <summary>
/// Parses scanner ABO/RhD structures (=%ggre). Does not infer from display text like "O POS".
/// Lookup decoding is supplied by the caller from versioned tables.
/// </summary>
public static class AboRhdParser
{
    public sealed record LookupRow(
        string Code,
        AboGroup Abo,
        RhType RhD,
        string? CollectionType,
        string? SpecialMessage,
        string? AdditionalPhenotype,
        DateOnly? EffectiveDate,
        DateOnly? RetiredDate,
        string StandardVersion);

    public static ParseOutcome<AboRhdParseResult> ParseScanner(
        string? input,
        IReadOnlyDictionary<string, LookupRow> lookup,
        bool allowHistoricalRetired = false,
        DateOnly? asOf = null,
        ScannerInputSanitizer.Options? sanitizeOptions = null)
    {
        var sanitizedResult = ScannerInputSanitizer.Sanitize(input, sanitizeOptions);
        var sanitized = sanitizedResult.Sanitized;

        if (!sanitized.StartsWith("=%", StringComparison.Ordinal))
        {
            return ParseOutcome<AboRhdParseResult>.Fail(
                IsbtErrorCodes.UnsupportedDataStructure,
                "ABO/RhD scanner value must start with '=%'.");
        }

        var code = sanitized[2..];
        if (code.Length == 0)
        {
            return ParseOutcome<AboRhdParseResult>.Fail(
                IsbtErrorCodes.UnknownAboRhdCode,
                "ABO/RhD code is empty.");
        }

        if (!lookup.TryGetValue(code, out var row))
        {
            return ParseOutcome<AboRhdParseResult>.Fail(
                IsbtErrorCodes.UnknownAboRhdCode,
                $"Unrecognized ABO/RhD code '{code}'. INSTITUTIONAL_POLICY_REVIEW: historical-code policy.");
        }

        var today = asOf ?? DateOnly.FromDateTime(DateTime.UtcNow);
        if (row.RetiredDate is not null && row.RetiredDate < today && !allowHistoricalRetired)
        {
            return ParseOutcome<AboRhdParseResult>.Fail(
                IsbtErrorCodes.UnknownAboRhdCode,
                $"ABO/RhD code '{code}' is retired and not accepted for new entry.");
        }

        return ParseOutcome<AboRhdParseResult>.Ok(new AboRhdParseResult(
            AboRhdCode: code,
            Abo: row.Abo,
            RhD: row.RhD,
            DonationCollectionCategory: row.CollectionType,
            EncodedPhenotype: row.AdditionalPhenotype,
            SpecialMessage: row.SpecialMessage,
            RawScan: sanitizedResult.Original,
            Sanitized: sanitized,
            FromScanner: true));
    }

    /// <summary>
    /// Builds encoded representation from structured human selection via reverse lookup.
    /// Does not invent codes — requires an exact lookup match.
    /// </summary>
    public static ParseOutcome<AboRhdParseResult> FromStructured(
        string aboRhdCode,
        IReadOnlyDictionary<string, LookupRow> lookup,
        bool allowHistoricalRetired = false,
        DateOnly? asOf = null)
    {
        return ParseScanner("=%" + aboRhdCode, lookup, allowHistoricalRetired, asOf);
    }
}
