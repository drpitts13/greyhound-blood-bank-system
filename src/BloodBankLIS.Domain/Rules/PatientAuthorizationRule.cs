namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Privilege gates for patient demographic writes used at identification and issue.
/// </summary>
public static class PatientAuthorizationRule
{
    public const string WriteCode = "PAT-WRITE-PERM";
    public const string CreateCode = "PAT-CREATE-PERM";

    public static RuleResult EvaluateWrite(bool hasPatientWrite) =>
        hasPatientWrite
            ? RuleResult.Pass(WriteCode)
            : RuleResult.HardStop(
                WriteCode,
                "Updating patient demographics requires the patient.write permission.");

    public static RuleResult EvaluateCreate(bool hasPatientWrite) =>
        hasPatientWrite
            ? RuleResult.Pass(CreateCode)
            : RuleResult.HardStop(
                CreateCode,
                "Creating a patient record requires the patient.write permission.");
}
