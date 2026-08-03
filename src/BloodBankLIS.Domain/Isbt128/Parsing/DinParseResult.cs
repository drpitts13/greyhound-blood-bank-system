namespace BloodBankLIS.Domain.Isbt128.Parsing;

public sealed record DinParseResult(
    string Din,
    string Fin,
    string NominalYear,
    string DonationSequence,
    string Flags,
    string? KeyboardCheck,
    string? RawScan,
    string Sanitized,
    bool FromScanner);
