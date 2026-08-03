using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Isbt128.Parsing;

public sealed record AboRhdParseResult(
    string AboRhdCode,
    AboGroup Abo,
    RhType RhD,
    string? DonationCollectionCategory,
    string? EncodedPhenotype,
    string? SpecialMessage,
    string? RawScan,
    string Sanitized,
    bool FromScanner);
