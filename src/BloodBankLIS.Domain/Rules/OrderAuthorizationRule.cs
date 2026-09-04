namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Privilege gates for workspace order mutations.
/// Inbound HL7 ORM and allocation-created crossmatch orders use ungated create paths.
/// </summary>
public static class OrderAuthorizationRule
{
    public const string CreateCode = "ORD-CREATE-PERM";
    public const string UpdateCode = "ORD-UPD-PERM";
    public const string CancelCode = "ORD-CXL-PERM";
    public const string LinkCode = "ORD-LINK-PERM";

    public static RuleResult EvaluateCreate(bool hasPatientWrite) =>
        hasPatientWrite
            ? RuleResult.Pass(CreateCode)
            : RuleResult.HardStop(
                CreateCode,
                "Creating an order requires the patient.write permission.");

    public static RuleResult EvaluateUpdate(bool hasPatientWrite) =>
        hasPatientWrite
            ? RuleResult.Pass(UpdateCode)
            : RuleResult.HardStop(
                UpdateCode,
                "Updating an order requires the patient.write permission.");

    public static RuleResult EvaluateCancel(bool hasPatientWrite) =>
        hasPatientWrite
            ? RuleResult.Pass(CancelCode)
            : RuleResult.HardStop(
                CancelCode,
                "Cancelling an order requires the patient.write permission.");

    public static RuleResult EvaluateLink(bool hasPatientWrite) =>
        hasPatientWrite
            ? RuleResult.Pass(LinkCode)
            : RuleResult.HardStop(
                LinkCode,
                "Linking a specimen to an order requires the patient.write permission.");
}
