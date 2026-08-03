namespace BloodBankLIS.Domain.Isbt128.Parsing;

/// <summary>
/// Parses scanner product structures (=&lt;αooootds) into PDC(5)+collection(1)+division(2).
/// </summary>
public static class ProductParser
{
    public sealed record LookupRow(
        string ProductDescriptionCode,
        string Description,
        string ComponentClass,
        string? Modifier,
        IReadOnlyList<string> Attributes,
        string? StorageRequirements,
        bool RequiresExtendedDivision,
        DateOnly? EffectiveDate,
        DateOnly? RetiredDate,
        string StandardVersion);

    public static ParseOutcome<ProductParseResult> ParseScanner(
        string? input,
        IReadOnlyDictionary<string, LookupRow> lookupByPdc,
        bool allowRetiredForExistingInventory = false,
        bool isNewManufactureOrRelabel = true,
        DateOnly? asOf = null,
        string? extendedDivisionCode = null,
        ScannerInputSanitizer.Options? sanitizeOptions = null)
    {
        var sanitizedResult = ScannerInputSanitizer.Sanitize(input, sanitizeOptions);
        var sanitized = sanitizedResult.Sanitized;

        if (!sanitized.StartsWith("=<", StringComparison.Ordinal))
        {
            return ParseOutcome<ProductParseResult>.Fail(
                IsbtErrorCodes.UnsupportedDataStructure,
                "Product scanner value must start with '=<'.");
        }

        var data = sanitized[2..];
        if (data.Length != 8)
        {
            return ParseOutcome<ProductParseResult>.Fail(
                IsbtErrorCodes.UnknownProductCode,
                "Product data must be exactly 8 characters after '=<'.");
        }

        var pdc = data[..5];
        var collection = data[5..6];
        var division = data[6..8];

        lookupByPdc.TryGetValue(pdc, out var row);
        var isRetired = row?.RetiredDate is not null && row.RetiredDate < (asOf ?? DateOnly.FromDateTime(DateTime.UtcNow));

        if (row is null)
        {
            return ParseOutcome<ProductParseResult>.Fail(
                IsbtErrorCodes.UnknownProductCode,
                $"Unrecognized product description code '{pdc}'. ICCBBA_VALIDATION_REQUIRED.");
        }

        if (isRetired && isNewManufactureOrRelabel && !allowRetiredForExistingInventory)
        {
            return ParseOutcome<ProductParseResult>.Fail(
                IsbtErrorCodes.RetiredProductNotAllowed,
                $"Retired product code '{pdc}' cannot be assigned to newly manufactured or relabeled components.");
        }

        var requiresExt = row.RequiresExtendedDivision;
        if (requiresExt && string.IsNullOrWhiteSpace(extendedDivisionCode))
        {
            return ParseOutcome<ProductParseResult>.Fail(
                IsbtErrorCodes.ExtendedDivisionRequired,
                "Extended division structure is required for this product division.");
        }

        return ParseOutcome<ProductParseResult>.Ok(new ProductParseResult(
            ProductCodeData: data,
            ProductDescriptionCode: pdc,
            CollectionTypeCode: collection,
            DivisionCode: division,
            ExtendedDivisionCode: extendedDivisionCode,
            ProductDescription: row.Description,
            ComponentClass: row.ComponentClass,
            Modifier: row.Modifier,
            Attributes: row.Attributes,
            RequiresExtendedDivision: requiresExt,
            IsRetired: isRetired,
            RawScan: sanitizedResult.Original,
            Sanitized: sanitized,
            FromScanner: true));
    }

    public static ParseOutcome<ProductParseResult> FromStructured(
        string productDescriptionCode,
        string collectionTypeCode,
        string divisionCode,
        IReadOnlyDictionary<string, LookupRow> lookupByPdc,
        string? extendedDivisionCode = null,
        bool allowRetiredForExistingInventory = false,
        bool isNewManufactureOrRelabel = true,
        DateOnly? asOf = null)
    {
        var pdc = (productDescriptionCode ?? string.Empty).Trim();
        var coll = (collectionTypeCode ?? string.Empty).Trim();
        var div = (divisionCode ?? string.Empty).Trim();
        if (pdc.Length != 5 || coll.Length != 1 || div.Length != 2)
        {
            return ParseOutcome<ProductParseResult>.Fail(
                IsbtErrorCodes.UnknownProductCode,
                "Structured product entry requires PDC(5), collection(1), and division(2).");
        }

        return ParseScanner(
            "=<" + pdc + coll + div,
            lookupByPdc,
            allowRetiredForExistingInventory,
            isNewManufactureOrRelabel,
            asOf,
            extendedDivisionCode);
    }
}
