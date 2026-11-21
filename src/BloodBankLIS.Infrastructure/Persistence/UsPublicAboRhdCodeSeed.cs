using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Infrastructure.Persistence;

/// <summary>
/// The eight standard ISBT 128 ABO/RhD group codes (data structure 002, <c>=%ggre</c>) with no additional typing
/// (r = 0, e = 0), as published in the UK Red Book (JPAC) chapter 23 table 23.1 and the Victoria (Australia) ISBT 128
/// resources handout (end-user testing loop 2, 2026-11-20). Placeholder rows like the US product-code subset: a
/// facility replaces or extends them with a licensed ICCBBA extract (OCD-004). Any other code, including one carrying
/// additional typing, stays unknown and is refused at receipt.
/// </summary>
public static class UsPublicAboRhdCodeSeed
{
    public const string StandardVersion = "PUBLIC-SUBSET-PENDING-ICCBBA";

    public static IReadOnlyList<IsbtAboRhdCode> CreateRows() =>
    [
        Row("5100", AboGroup.O, RhType.Positive),
        Row("9500", AboGroup.O, RhType.Negative),
        Row("6200", AboGroup.A, RhType.Positive),
        Row("0600", AboGroup.A, RhType.Negative),
        Row("7300", AboGroup.B, RhType.Positive),
        Row("1700", AboGroup.B, RhType.Negative),
        Row("8400", AboGroup.AB, RhType.Positive),
        Row("2800", AboGroup.AB, RhType.Negative)
    ];

    private static IsbtAboRhdCode Row(string code, AboGroup abo, RhType rh) => new()
    {
        Code = code,
        Abo = abo,
        RhD = rh,
        SpecialMessage = "Public subset pending ICCBBA validation",
        StandardVersion = StandardVersion,
        IsPlaceholder = true
    };
}
