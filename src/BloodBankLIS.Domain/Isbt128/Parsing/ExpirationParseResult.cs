namespace BloodBankLIS.Domain.Isbt128.Parsing;

public sealed record ExpirationParseResult(
    string ExpirationEncoded,
    DateTime ExpirationLocal,
    string ExpirationTimezone,
    bool ExpirationHasExplicitTime,
    string CenturyIndicator,
    int Year,
    int OrdinalDay,
    int? Hour,
    int? Minute,
    string? RawScan,
    string Sanitized,
    bool FromScanner);
