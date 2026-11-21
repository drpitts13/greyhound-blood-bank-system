using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// A test whose verified result is the patient's ABO/Rh type: ABORH, or any ABO/Rh test set to contribute to the ABO/Rh
/// history (the neonatal type and screen). End-user testing passes 122 and 123: only ABORH was treated as typing the
/// patient, so the neonatal type left no type on the chart, was not flagged when it disagreed with the type on file,
/// and could be verified by the person who entered it.
/// </summary>
public static class PatientTypingTestRule
{
    public const string AboRhCode = "ABORH";

    public static bool Is(string? testCode, ResultValueType? resultValueType, bool contributesToAboRhHistory) =>
        string.Equals(testCode, AboRhCode, StringComparison.OrdinalIgnoreCase)
        || (resultValueType == ResultValueType.AboRh && contributesToAboRhHistory);
}
