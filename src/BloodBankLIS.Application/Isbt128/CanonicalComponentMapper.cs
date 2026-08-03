using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Isbt128;
using BloodBankLIS.Domain.Isbt128.Parsing;

namespace BloodBankLIS.Application.Isbt128;

/// <summary>Maps a validated canonical draft onto a BloodUnit entity and raw-scan rows.</summary>
public static class CanonicalComponentMapper
{
    public static BloodUnit ToBloodUnit(
        CanonicalComponentDraft draft,
        long productTypeId,
        long? locationId,
        string? supplier,
        string? shipmentId,
        string? collectionFacility,
        decimal? volume,
        UnitStatus initialStatus,
        DateTime utcNow)
    {
        draft.RebuildIdentity();
        if (draft.Din is null || draft.Product is null || draft.AboRhd is null || draft.Expiration is null)
            throw new InvalidOperationException("Draft is missing required quadrants.");

        var identity = draft.ComponentIdentity
            ?? ComponentIdentityBuilder.Build(draft.Din.Din, draft.Product.ProductCodeData, draft.Product.ExtendedDivisionCode);

        var expirationUtc = DateTime.SpecifyKind(draft.Expiration.ExpirationLocal, DateTimeKind.Utc);
        try
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById(draft.Expiration.ExpirationTimezone);
            expirationUtc = TimeZoneInfo.ConvertTimeToUtc(
                DateTime.SpecifyKind(draft.Expiration.ExpirationLocal, DateTimeKind.Unspecified), tz);
        }
        catch
        {
            // INSTITUTIONAL_POLICY_REVIEW: facility timezone configuration.
            expirationUtc = DateTime.SpecifyKind(draft.Expiration.ExpirationLocal, DateTimeKind.Utc);
        }

        var unit = new BloodUnit
        {
            UnitNumber = identity,
            ComponentIdentity = identity,
            ComponentIdentityKey = ComponentIdentityBuilder.BuildUniquenessKey(
                draft.Din.Din, draft.Product.ProductCodeData, draft.Product.ExtendedDivisionCode),
            ProductTypeId = productTypeId,
            Abo = draft.AboRhd.Abo,
            RhD = draft.AboRhd.RhD,
            Din = draft.Din.Din,
            Fin = draft.Din.Fin,
            NominalYear = draft.Din.NominalYear,
            DonationSequence = draft.Din.DonationSequence,
            DinFlags = draft.Din.Flags,
            DinKeyboardCheck = draft.Din.KeyboardCheck,
            AboRhdCode = draft.AboRhd.AboRhdCode,
            DonationCollectionCategory = draft.AboRhd.DonationCollectionCategory,
            EncodedPhenotype = draft.AboRhd.EncodedPhenotype,
            AboSpecialMessage = draft.AboRhd.SpecialMessage,
            ProductCodeData = draft.Product.ProductCodeData,
            ProductDescriptionCode = draft.Product.ProductDescriptionCode,
            CollectionTypeCode = draft.Product.CollectionTypeCode,
            DivisionCode = draft.Product.DivisionCode,
            ExtendedDivisionCode = draft.Product.ExtendedDivisionCode,
            Isbt128DonationId = draft.Din.Din,
            Isbt128ProductCode = draft.Product.ProductCodeData,
            ExpirationEncoded = draft.Expiration.ExpirationEncoded,
            ExpirationLocal = draft.Expiration.ExpirationLocal,
            ExpirationTimezone = draft.Expiration.ExpirationTimezone,
            ExpirationHasExplicitTime = draft.Expiration.ExpirationHasExplicitTime,
            ExpiresUtc = expirationUtc,
            CollectionDateTime = draft.CollectionDateTime,
            ProcessingFacilityCode = draft.ProcessingFacilityCode,
            StandardVersion = draft.StandardVersion,
            Source = draft.Source,
            CurrentLocationId = locationId,
            Supplier = supplier,
            ShipmentId = shipmentId,
            CollectionFacility = collectionFacility,
            Volume = volume,
            Status = initialStatus
        };

        AddRawScan(unit, draft.Din.RawScan, draft.Din.Sanitized, draft.Din.Din, IsbtDataStructureKind.DonationIdentificationNumber, draft, utcNow);
        AddRawScan(unit, draft.AboRhd.RawScan, draft.AboRhd.Sanitized, draft.AboRhd.AboRhdCode, IsbtDataStructureKind.AboRhd, draft, utcNow);
        AddRawScan(unit, draft.Product.RawScan, draft.Product.Sanitized, draft.Product.ProductCodeData, IsbtDataStructureKind.ProductCode, draft, utcNow);
        AddRawScan(
            unit,
            draft.Expiration.RawScan,
            draft.Expiration.Sanitized,
            draft.Expiration.ExpirationEncoded,
            draft.Expiration.ExpirationHasExplicitTime
                ? IsbtDataStructureKind.ExpirationDateTime
                : IsbtDataStructureKind.ExpirationDate,
            draft,
            utcNow);

        return unit;
    }

    public static CanonicalComponentSummary ToSummary(CanonicalComponentDraft draft)
    {
        draft.RebuildIdentity();
        return new CanonicalComponentSummary(
            draft.ComponentIdentity,
            draft.Din?.Din,
            draft.Din?.Flags,
            draft.AboRhd?.AboRhdCode,
            draft.AboRhd?.Abo.ToString(),
            draft.AboRhd?.RhD.ToString(),
            draft.Product?.ProductCodeData,
            draft.Product?.ProductDescription,
            draft.Expiration?.ExpirationEncoded,
            draft.Expiration?.ExpirationLocal,
            draft.Expiration?.ExpirationHasExplicitTime,
            draft.HasRequiredQuadrants);
    }

    private static void AddRawScan(
        BloodUnit unit,
        string? original,
        string? sanitized,
        string? normalized,
        IsbtDataStructureKind kind,
        CanonicalComponentDraft draft,
        DateTime utcNow)
    {
        if (string.IsNullOrEmpty(original) && string.IsNullOrEmpty(sanitized))
            return;

        unit.RawScans.Add(new BloodComponentRawScan
        {
            StructureKind = kind,
            OriginalValue = original ?? sanitized ?? string.Empty,
            SanitizedValue = sanitized ?? original ?? string.Empty,
            NormalizedValue = normalized,
            Source = draft.Source,
            EnteredBy = draft.EnteredBy,
            EnteredAt = utcNow
        });
    }
}
