using BloodBankLIS.Domain.Isbt128;
using BloodBankLIS.Domain.Isbt128.Parsing;

namespace BloodBankLIS.Application.Isbt128;

/// <summary>
/// Validates supplier product codes against the ISBT product description code lookup.
/// Accepts a 5-character PDC or an 8-character ProductCodeData (PDC + collection + division).
/// </summary>
public static class ProductCodeLookupValidator
{
    public sealed record ResolvedProductCode(
        string ProductDescriptionCode,
        string? ProductCodeData,
        string? CollectionTypeCode,
        string? DivisionCode,
        string Description);

    public static (bool Success, ResolvedProductCode? Value, string? Error) Validate(
        string? productCodeInput,
        IReadOnlyDictionary<string, ProductParser.LookupRow> lookupByPdc,
        DateOnly? asOf = null)
    {
        var raw = (productCodeInput ?? string.Empty).Trim().ToUpperInvariant();
        if (raw.Length == 0)
        {
            return (false, null,
                $"{IsbtErrorCodes.UnknownProductCode}: Product description code is required.");
        }

        string pdc;
        string? productCodeData = null;
        string? collection = null;
        string? division = null;

        if (raw.Length == 5)
        {
            pdc = raw;
        }
        else if (raw.Length == 8)
        {
            pdc = raw[..5];
            collection = raw[5..6];
            division = raw[6..8];
            productCodeData = raw;
        }
        else
        {
            return (false, null,
                $"{IsbtErrorCodes.UnknownProductCode}: Product code must be a 5-character PDC or 8-character product code data.");
        }

        if (!lookupByPdc.TryGetValue(pdc, out var row))
        {
            return (false, null,
                $"{IsbtErrorCodes.UnknownProductCode}: Unrecognized product description code '{pdc}'.");
        }

        var today = asOf ?? DateOnly.FromDateTime(DateTime.UtcNow);
        if (row.RetiredDate is not null && row.RetiredDate < today)
        {
            return (false, null,
                $"{IsbtErrorCodes.RetiredProductNotAllowed}: Retired product code '{pdc}' cannot be assigned to newly received units.");
        }

        return (true, new ResolvedProductCode(pdc, productCodeData, collection, division, row.Description), null);
    }
}
