using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Instruments;

/// <summary>Classifies an ASTM R record as assay-level interpretation or analyte-level result.</summary>
public static class InstrumentInterpretationClassifier
{
    public static bool IsInterpretation(string? analyte, string? assay, InstrumentInterface? iface) =>
        IsInterpretation(analyte, assay, iface?.InterpretationMatch ?? InstrumentInterpretationMatch.AnalyteEqualsAssay,
            iface?.InterpretationAnalyteCodes);

    public static bool IsInterpretation(
        string? analyte,
        string? assay,
        InstrumentInterpretationMatch match,
        string? codes)
    {
        if (string.IsNullOrWhiteSpace(analyte))
        {
            return false;
        }

        var analyteCode = analyte.Trim();
        return match switch
        {
            InstrumentInterpretationMatch.AnalyteEqualsAssay =>
                !string.IsNullOrWhiteSpace(assay)
                && string.Equals(analyteCode, assay.Trim(), StringComparison.OrdinalIgnoreCase),
            InstrumentInterpretationMatch.AnalyteInList =>
                ParseCodes(codes).Contains(analyteCode, StringComparer.OrdinalIgnoreCase),
            InstrumentInterpretationMatch.Never => false,
            _ => false
        };
    }

    public static IReadOnlyList<string> ParseCodes(string? codes)
    {
        if (string.IsNullOrWhiteSpace(codes))
        {
            return [];
        }

        return codes.Split([',', ';', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}
