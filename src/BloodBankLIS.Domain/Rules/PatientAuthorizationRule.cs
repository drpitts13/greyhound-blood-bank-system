namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Privilege gates for patient demographic writes used at identification and issue.
/// </summary>
public static class PatientAuthorizationRule
{
    public const string WriteCode = "PAT-WRITE-PERM";

    public static RuleResult EvaluateWrite(bool hasPatientWrite) =>
        hasPatientWrite
            ? RuleResult.Pass(WriteCode)
            : RuleResult.HardStop(
                WriteCode,
                "Updating patient demographics requires the patient.write permission.");
}
