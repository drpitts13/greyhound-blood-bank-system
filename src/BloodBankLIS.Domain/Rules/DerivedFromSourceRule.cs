namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// What a result or a specimen taken out of use does to what was derived from it. A blood type recorded from a
/// result that is invalidated stops being the patient's current type; no earlier type is reinstated, so the type
/// must be repeated. A specimen cannot be rejected while it still has active results: they are invalidated first,
/// one by one, so their consequences (including the blood type) follow the result invalidation path.
/// </summary>
public static class DerivedFromSourceRule
{
    public const string TypeRetiredCode = "RES-TYPE-RETIRED";
    public const string RejectWithResultsCode = "SPEC-REJECT-RESULTS";
    public const string RejectReservedUnitsCode = "SPEC-REJECT-RESERVED";

    public static RuleResult TypeRetired(string aboRh) =>
        RuleResult.Warning(TypeRetiredCode,
            $"The patient's current ABO/Rh ({aboRh}) came from this result and is no longer current. Repeat the ABO/Rh before red cells are issued.");

    public static RuleResult EvaluateReject(IReadOnlyList<string> activeResultTests) =>
        activeResultTests.Count == 0
            ? RuleResult.Pass(RejectWithResultsCode)
            : RuleResult.HardStop(RejectWithResultsCode,
                $"This specimen still has results ({string.Join(", ", activeResultTests)}). Invalidate them first, then reject the specimen, so nothing derived from it stays in use.");

    public static RuleResult ReservedUnits(IReadOnlyList<string> unitNumbers) =>
        RuleResult.Warning(RejectReservedUnitsCode,
            $"Units are still reserved against this specimen: {string.Join(", ", unitNumbers)}. Review and release them on Compatibility.");
}
