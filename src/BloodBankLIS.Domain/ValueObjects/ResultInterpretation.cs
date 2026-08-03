namespace BloodBankLIS.Domain.ValueObjects;

/// <summary>
/// Resolves the human-readable interpretation of a test result for rule evaluation.
/// <c>TestResult.Interpretation</c> is optional and is normally left null on the ABO/Rh
/// entry path, where the typed result lives in <c>TestResult.Value</c> as either the
/// "Abo|Rh" key or the panel JSON. Rules must see the same value in every case, so the
/// interpretation is derived when it was not entered explicitly.
/// </summary>
public static class ResultInterpretation
{
    /// <summary>Canonical display form used by rule conditions, e.g. "A Negative".</summary>
    public static string Format(AboRh type) => $"{type.Abo} {type.Rh}";

    public static string? Resolve(string? interpretation, string? value)
    {
        if (!string.IsNullOrWhiteSpace(interpretation))
        {
            var trimmed = interpretation.Trim();

            // An explicitly stored "A|Negative" is normalized to the canonical form.
            return AboRhResultValue.TryParse(trimmed, out var stored) && stored.IsKnown
                ? Format(stored)
                : trimmed;
        }

        if (AboRhResultValue.TryParse(value, out var derived) && derived.IsKnown)
        {
            return Format(derived);
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        // Panel JSON that carries no interpreted type is not a meaningful interpretation.
        return value.TrimStart().StartsWith('{') ? null : value.Trim();
    }

    /// <summary>Subtest reaction grades recorded on a panel result, keyed by subtest code.</summary>
    public static IReadOnlyDictionary<string, string> ResolveSubtests(string? value)
    {
        if (AboRhResultValue.TryParsePanel(value, out var aboRhPanel))
        {
            return aboRhPanel.Subtests;
        }

        return PanelResultValue.TryParse(value, out var subtests)
            ? subtests
            : new Dictionary<string, string>();
    }
}
