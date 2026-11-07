namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// OCD-040: before a returned unit can go back to available stock, a person must record its temperature, seal,
/// and visual inspection. An unanswered inspection is refused when the unit could return to Available, unless the
/// facility treats unanswered inspections as acceptable (<see cref="FacilityPolicyKeys.InspectionDefaultAcceptable"/>,
/// default off). A unit sent explicitly to Quarantine or Disposed may leave an inspection unanswered; it is recorded
/// as unanswered, not as passed. Time out of storage is not an input: the computer evaluates it
/// (<see cref="ReturnReissueRule.TimeOutAcceptable"/>).
/// </summary>
public static class ReturnInspectionRule
{
    public const string Code = "RET-INSPECT";

    public sealed record Resolved(
        RuleResult Result,
        bool TemperatureAcceptable,
        bool SealIntegrityAcceptable,
        bool VisualInspectionAcceptable,
        bool AnyUnanswered,
        bool UsedFacilityDefault);

    /// <param name="mayReturnToAvailable">True when the destination is Available or left for the computer to decide.</param>
    public static Resolved Resolve(bool? temperature, bool? seal, bool? visual, bool facilityDefaultAcceptable, bool mayReturnToAvailable)
    {
        var missing = new List<string>();
        if (temperature is null) missing.Add("temperature");
        if (seal is null) missing.Add("seal integrity");
        if (visual is null) missing.Add("visual inspection");

        if (missing.Count > 0 && mayReturnToAvailable && !facilityDefaultAcceptable)
        {
            return new Resolved(
                RuleResult.HardStop(Code, $"Record the return inspection before the unit can go back to available stock: {string.Join(", ", missing)}."),
                false, false, false, true, false);
        }

        // An unanswered check never fails the unit by itself; it only cannot make it available unless the facility
        // default applies. Explicit quarantine or disposal keeps it out of stock either way.
        return new Resolved(
            RuleResult.Pass(Code),
            temperature ?? true,
            seal ?? true,
            visual ?? true,
            missing.Count > 0,
            missing.Count > 0 && facilityDefaultAcceptable);
    }

    public static bool AllAnswered(bool? temperature, bool? seal, bool? visual) =>
        temperature is not null && seal is not null && visual is not null;
}
