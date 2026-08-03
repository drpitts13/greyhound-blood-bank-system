using System.Text.RegularExpressions;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Isbt128.Parsing;

/// <summary>
/// Parses scanner-formatted DIN (=DIN13 + flag2) and human-readable DIN entry.
/// Flag characters are not part of the DIN. Case is preserved.
/// </summary>
public static class DinParser
{
    private static readonly Regex DinCharSet = new("^[A-Za-z0-9]{13}$", RegexOptions.Compiled);
    private static readonly Regex HumanReadable = new(
        @"^(?<fin>[A-Za-z0-9]{5})\s*(?<year>\d{2})\s*(?<seq>\d{6})(?:\s+(?<check>[A-Za-z0-9*]))?$",
        RegexOptions.Compiled);

    public static ParseOutcome<DinParseResult> Parse(
        string? input,
        IDinCheckCharacterValidator? checkValidator = null,
        bool requireKeyboardCheck = false,
        ScannerInputSanitizer.Options? sanitizeOptions = null)
    {
        var sanitizedResult = ScannerInputSanitizer.Sanitize(input, sanitizeOptions);
        var sanitized = sanitizedResult.Sanitized;
        if (string.IsNullOrWhiteSpace(sanitized))
            return ParseOutcome<DinParseResult>.Fail(IsbtErrorCodes.InvalidDinLength, "DIN input is empty.");

        var mode = IsbtInputTypeDetector.Detect(sanitized, sanitizeOptions);
        if (mode == IsbtInputMode.ScannedIsbt || sanitized.StartsWith('='))
            return ParseScanner(sanitizedResult.Original, sanitized);

        return ParseHumanReadable(sanitizedResult.Original, sanitized, checkValidator, requireKeyboardCheck);
    }

    public static ParseOutcome<DinParseResult> ParseStructured(
        string fin,
        string year,
        string sequence,
        string? keyboardCheck,
        IDinCheckCharacterValidator? checkValidator = null,
        bool requireKeyboardCheck = false)
    {
        var finT = (fin ?? string.Empty).Trim();
        var yearT = (year ?? string.Empty).Trim();
        var seqT = (sequence ?? string.Empty).Trim();
        var combined = $"{finT}{yearT}{seqT}";
        return ParseHumanReadable(combined, $"{finT} {yearT} {seqT}" + (keyboardCheck is null ? "" : $" {keyboardCheck}"), checkValidator, requireKeyboardCheck, keyboardCheck);
    }

    private static ParseOutcome<DinParseResult> ParseScanner(string original, string sanitized)
    {
        if (!sanitized.StartsWith('=') || sanitized.StartsWith("=%") || sanitized.StartsWith("=<")
            || sanitized.StartsWith("=>") || sanitized.StartsWith("=*"))
        {
            return ParseOutcome<DinParseResult>.Fail(
                IsbtErrorCodes.UnsupportedDataStructure,
                "Value is not a DIN data structure (expected leading '=' alone).");
        }

        var payload = sanitized[1..];
        if (payload.Length < 15)
            return ParseOutcome<DinParseResult>.Fail(IsbtErrorCodes.InvalidDinLength, "Scanner DIN must be DIN13 + 2 flag characters.");

        if (payload.Length > 15)
            return ParseOutcome<DinParseResult>.Fail(IsbtErrorCodes.InvalidDinLength, "Scanner DIN payload longer than expected (15 characters after '=').");

        var din = payload[..13];
        var flags = payload[13..];

        if (!DinCharSet.IsMatch(din))
            return ParseOutcome<DinParseResult>.Fail(IsbtErrorCodes.InvalidDinCharacter, "DIN contains invalid characters.");

        if (flags.Length != 2)
            return ParseOutcome<DinParseResult>.Fail(IsbtErrorCodes.InvalidFlagLength, "Flag characters must be exactly 2 characters.");

        return ParseOutcome<DinParseResult>.Ok(new DinParseResult(
            Din: din,
            Fin: din[..5],
            NominalYear: din[5..7],
            DonationSequence: din[7..13],
            Flags: flags,
            KeyboardCheck: null,
            RawScan: original,
            Sanitized: sanitized,
            FromScanner: true));
    }

    private static ParseOutcome<DinParseResult> ParseHumanReadable(
        string original,
        string sanitized,
        IDinCheckCharacterValidator? checkValidator,
        bool requireKeyboardCheck,
        string? explicitCheck = null)
    {
        // Remove only visual formatting spaces.
        var spaced = Regex.Replace(sanitized.Trim(), @"\s+", " ");
        var match = HumanReadable.Match(spaced);
        string din;
        string? check = explicitCheck?.Trim();

        if (match.Success)
        {
            din = match.Groups["fin"].Value + match.Groups["year"].Value + match.Groups["seq"].Value;
            if (match.Groups["check"].Success)
                check = match.Groups["check"].Value;
        }
        else
        {
            var compact = spaced.Replace(" ", "");
            if (compact.Length == 14)
            {
                check = compact[^1].ToString();
                din = compact[..13];
            }
            else if (compact.Length == 13)
            {
                din = compact;
            }
            else
            {
                return ParseOutcome<DinParseResult>.Fail(
                    IsbtErrorCodes.InvalidDinLength,
                    "Human-readable DIN must be 13 characters (optionally with keyboard check).");
            }
        }

        if (!DinCharSet.IsMatch(din))
            return ParseOutcome<DinParseResult>.Fail(IsbtErrorCodes.InvalidDinCharacter, "DIN contains invalid characters.");

        if (requireKeyboardCheck && string.IsNullOrEmpty(check))
        {
            return ParseOutcome<DinParseResult>.Fail(
                IsbtErrorCodes.DinCheckMismatch,
                "Keyboard check character is required for manual DIN entry.");
        }

        if (!string.IsNullOrEmpty(check) && checkValidator is not null)
        {
            if (!checkValidator.IsValid(din, check[0]))
            {
                return ParseOutcome<DinParseResult>.Fail(
                    IsbtErrorCodes.DinCheckMismatch,
                    "DIN keyboard check character does not match.");
            }
        }

        return ParseOutcome<DinParseResult>.Ok(new DinParseResult(
            Din: din,
            Fin: din[..5],
            NominalYear: din[5..7],
            DonationSequence: din[7..13],
            Flags: "00",
            KeyboardCheck: check,
            RawScan: original,
            Sanitized: sanitized,
            FromScanner: false));
    }
}
