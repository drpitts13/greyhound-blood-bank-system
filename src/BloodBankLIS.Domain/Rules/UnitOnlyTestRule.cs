namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Tests performed on a blood unit, not on a patient: the unit ABO/Rh retypes and unit antigen typing. A patient order
/// does not offer them (end-user testing pass 242: Add order listed "Rh+ Retype" and "Unit Antigen Typing" beside the
/// patient's ABO/Rh and screen). They are recorded from the unit (Retype desk, unit antigen typing).
/// </summary>
public static class UnitOnlyTestRule
{
    /// <summary>The unit antigen typing test code (as <c>UnitAntigenTypingService.TestCode</c>).</summary>
    public const string UnitAntigenTypingCode = "UAGTYPE";

    private static readonly HashSet<string> Codes = new(StringComparer.OrdinalIgnoreCase)
    {
        AboRhRetypeRule.TestCode,
        ProductRetypeAssignment.RhPositiveTestCode,
        ProductRetypeAssignment.RhNegativeTestCode,
        UnitAntigenTypingCode
    };

    public static bool IsUnitOnly(string? testCode) => testCode is not null && Codes.Contains(testCode.Trim());
}
