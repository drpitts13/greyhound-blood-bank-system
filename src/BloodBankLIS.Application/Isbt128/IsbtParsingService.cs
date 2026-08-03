using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Isbt128;
using BloodBankLIS.Domain.Isbt128.Parsing;

namespace BloodBankLIS.Application.Isbt128;

/// <summary>
/// Normalization entry point: sanitize → classify → structure-specific parse.
/// Downstream workflows must not re-parse raw barcode strings independently.
/// </summary>
public sealed class IsbtParsingService
{
    private readonly IsbtLookupCatalog _lookups;
    private readonly IDinCheckCharacterValidator _dinCheck;

    public IsbtParsingService(IsbtLookupCatalog lookups, IDinCheckCharacterValidator dinCheck)
    {
        _lookups = lookups;
        _dinCheck = dinCheck;
    }

    public async Task<ParseIsbtInputResponse> ParseAsync(string? value, CancellationToken ct = default)
    {
        var sanitized = ScannerInputSanitizer.Sanitize(value);
        var mode = IsbtInputTypeDetector.Detect(sanitized.Sanitized);
        var kind = IsbtDataStructureRegistry.Classify(sanitized.Sanitized);

        if (mode == IsbtInputMode.ScannedIsbt && kind == IsbtDataStructureKind.Unknown)
        {
            return new ParseIsbtInputResponse(
                mode, kind, sanitized.Original, sanitized.Sanitized, null,
                new[] { IsbtErrorCodes.UnsupportedDataStructure },
                new[] { "Unsupported ISBT data structure." });
        }

        object? parsed = null;
        var errorCodes = new List<string>();
        var errorMessages = new List<string>();

        switch (kind)
        {
            case IsbtDataStructureKind.DonationIdentificationNumber:
            {
                var result = DinParser.Parse(sanitized.Sanitized, _dinCheck);
                if (result.Success) parsed = result.Value;
                else Collect(result, errorCodes, errorMessages);
                break;
            }
            case IsbtDataStructureKind.AboRhd:
            {
                var lookup = await _lookups.GetAboLookupAsync(ct);
                var result = AboRhdParser.ParseScanner(sanitized.Sanitized, lookup);
                if (result.Success) parsed = result.Value;
                else Collect(result, errorCodes, errorMessages);
                break;
            }
            case IsbtDataStructureKind.ProductCode:
            {
                var lookup = await _lookups.GetProductLookupAsync(ct);
                var result = ProductParser.ParseScanner(sanitized.Sanitized, lookup);
                if (result.Success) parsed = result.Value;
                else Collect(result, errorCodes, errorMessages);
                break;
            }
            case IsbtDataStructureKind.ExpirationDate:
            case IsbtDataStructureKind.ExpirationDateTime:
            {
                var result = ExpirationParser.Parse(sanitized.Sanitized);
                if (result.Success) parsed = result.Value;
                else Collect(result, errorCodes, errorMessages);
                break;
            }
            case IsbtDataStructureKind.Unknown when mode == IsbtInputMode.HumanReadable:
            {
                // Attempt human-readable DIN.
                var result = DinParser.Parse(sanitized.Sanitized, _dinCheck, requireKeyboardCheck: false);
                if (result.Success)
                {
                    kind = IsbtDataStructureKind.DonationIdentificationNumber;
                    parsed = result.Value;
                }
                else Collect(result, errorCodes, errorMessages);
                break;
            }
        }

        return new ParseIsbtInputResponse(
            mode, kind, sanitized.Original, sanitized.Sanitized, parsed, errorCodes, errorMessages);
    }

    private static void Collect<T>(ParseOutcome<T> outcome, List<string> codes, List<string> messages)
    {
        foreach (var e in outcome.Errors)
        {
            codes.Add(e.Code);
            messages.Add(e.Message);
        }
    }
}
