namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Privilege gates for order mutations that are not invoked by inbound HL7 ORM.
/// Order create remains ungated in the service so the interface processor can post.
/// </summary>
public static class OrderAuthorizationRule
{
    public const string UpdateCode = "ORD-UPD-PERM";
    public const string CancelCode = "ORD-CXL-PERM";
    public const string LinkCode = "ORD-LINK-PERM";

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
