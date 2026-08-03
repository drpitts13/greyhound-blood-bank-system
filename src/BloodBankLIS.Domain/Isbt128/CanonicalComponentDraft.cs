using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Isbt128.Parsing;

namespace BloodBankLIS.Domain.Isbt128;

/// <summary>
/// In-memory canonical blood-component draft produced by the normalization layer.
/// Downstream workflows must use this (or the persisted BloodUnit fields), never
/// independently re-parse raw barcode strings.
/// </summary>
public sealed class CanonicalComponentDraft
{
    public string? ComponentIdentity { get; set; }

    public DinParseResult? Din { get; set; }
    public AboRhdParseResult? AboRhd { get; set; }
    public ProductParseResult? Product { get; set; }
    public ExpirationParseResult? Expiration { get; set; }

    public DateTime? CollectionDateTime { get; set; }
    public string? ProcessingFacilityCode { get; set; }

    public ComponentEntrySource Source { get; set; } = ComponentEntrySource.Scanner;
    public string EnteredBy { get; set; } = "system";
    public DateTime EnteredAt { get; set; }
    public string StandardVersion { get; set; } = "PLACEHOLDER-REQUIRES-ICCBBA";

    public string? RawDinScan => Din?.RawScan;
    public string? RawAboScan => AboRhd?.RawScan;
    public string? RawProductScan => Product?.RawScan;
    public string? RawExpirationScan => Expiration?.RawScan;

    public bool HasRequiredQuadrants =>
        Din is not null && AboRhd is not null && Product is not null && Expiration is not null;

    public void RebuildIdentity()
    {
        if (Din is null || Product is null)
        {
            ComponentIdentity = null;
            return;
        }

        ComponentIdentity = ComponentIdentityBuilder.Build(
            Din.Din,
            Product.ProductCodeData,
            Product.ExtendedDivisionCode);
    }
}
