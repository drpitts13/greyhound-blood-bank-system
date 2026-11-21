using BloodBankLIS.Domain.ValueObjects;

namespace BloodBankLIS.Domain.Instruments;

/// <summary>
/// An instrument interpretation that is already an LIS interpretation (end-user testing pass 148: the ImmuLINK Echo's
/// "Negative" and "Positive" screen and DAT interpretations posted as they came, but were listed as unmapped because the
/// instrument translates other interpretations, such as 'B Pos'). Positive, Negative, Compatible, Incompatible and an
/// ABO/Rh in the LIS's form are used as they come and are not listed.
/// </summary>
public static class InstrumentInterpretationValue
{
    private static readonly string[] Words = ["Positive", "Negative", "Compatible", "Incompatible"];

    public static bool IsLocal(string? interpretation)
    {
        if (string.IsNullOrWhiteSpace(interpretation))
        {
            return false;
        }

        var text = interpretation.Trim();
        return Words.Contains(text, StringComparer.OrdinalIgnoreCase) || AboRhResultValue.TryParse(text, out _);
    }
}
