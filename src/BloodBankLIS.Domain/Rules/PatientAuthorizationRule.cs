namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Privilege gates for patient demographic writes and workspace identity merge
/// used at identification and issue. Inbound ADT A18/A40 uses an ungated
/// interface merge path.
/// </summary>
public static class PatientAuthorizationRule
{
    public const string WriteCode = "PAT-WRITE-PERM";
    public const string CreateCode = "PAT-CREATE-PERM";
    public const string MergeCode = "PAT-MERGE-PERM";

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

    public static RuleResult EvaluateMerge(bool hasPatientMerge) =>
        hasPatientMerge
            ? RuleResult.Pass(MergeCode)
            : RuleResult.HardStop(
                MergeCode,
                "Merging a duplicate patient into a survivor requires the patient.merge permission.");
}
