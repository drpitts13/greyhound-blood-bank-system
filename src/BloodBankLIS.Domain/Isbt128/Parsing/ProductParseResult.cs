namespace BloodBankLIS.Domain.Isbt128.Parsing;

public sealed record ProductParseResult(
    string ProductCodeData,
    string ProductDescriptionCode,
    string CollectionTypeCode,
    string DivisionCode,
    string? ExtendedDivisionCode,
    string? ProductDescription,
    string? ComponentClass,
    string? Modifier,
    IReadOnlyList<string> Attributes,
    bool RequiresExtendedDivision,
    bool IsRetired,
    string? RawScan,
    string Sanitized,
    bool FromScanner);
