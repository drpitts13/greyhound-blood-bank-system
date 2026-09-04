namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Privilege gates for manual visit create/update. Inbound ADT uses
/// <c>UpsertVisitFromHl7Async</c> / <c>EnsureEncounterForHl7OrderAsync</c> and is not gated here.
/// </summary>
public static class EncounterAuthorizationRule
{
    public const string CreateCode = "ENC-CREATE-PERM";
    public const string UpdateCode = "ENC-UPD-PERM";

    public static RuleResult EvaluateCreate(bool hasPatientWrite) =>
        hasPatientWrite
            ? RuleResult.Pass(CreateCode)
            : RuleResult.HardStop(
                CreateCode,
                "Creating a visit requires the patient.write permission.");

    public static RuleResult EvaluateUpdate(bool hasPatientWrite) =>
        hasPatientWrite
            ? RuleResult.Pass(UpdateCode)
            : RuleResult.HardStop(
                UpdateCode,
                "Updating a visit requires the patient.write permission.");
}
