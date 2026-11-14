using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// AABB-style transfusion-reaction workup gate (clerical check, visual inspection,
/// post-transfusion DAT). SoftBank and SafeTrace block close until these steps are
/// recorded; HemoCore previously allowed close on free-text findings alone.
/// </summary>
public static class ReactionWorkupCompletenessRule
{
    public const string Code = "RXN-WORKUP-INCOMPLETE";

    /// <summary>
    /// The specimen workup: whether each specimen and its Test Group order are on the case, how many of each
    /// group's tests are not yet verified, and whether the verified post-transfusion DAT is positive.
    /// </summary>
    public sealed record WorkupState(
        bool PreLinked,
        bool PostLinked,
        int PrePending,
        int PostPending,
        bool PostDatPositive);

    /// <summary>
    /// The close gate with the specimen workup: clerical check, visual inspection, both workup specimens with their
    /// Test Group tests verified, then the elution when the verified post-transfusion DAT is positive. Each message
    /// names the next thing to do.
    /// </summary>
    public static RuleResult Evaluate(
        bool clericalCheckCompleted,
        bool visualInspectionCompleted,
        WorkupState workup,
        string? elutionResult)
    {
        ArgumentNullException.ThrowIfNull(workup);
        if (!clericalCheckCompleted)
            return RuleResult.HardStop(Code, "Clerical check (patient ID, unit number, ABO/Rh) has not been recorded.");

        if (!visualInspectionCompleted)
            return RuleResult.HardStop(Code, "Visual inspection of the returned unit or segments has not been recorded.");

        if (!workup.PreLinked)
            return RuleResult.HardStop(Code, "Choose the pre-transfusion specimen to order its workup tests.");

        if (!workup.PostLinked)
            return RuleResult.HardStop(Code, "Accession or choose the post-transfusion specimen to order its workup tests.");

        var pending = workup.PrePending + workup.PostPending;
        if (pending > 0)
            return RuleResult.HardStop(Code, $"Result and verify the workup tests: {pending} still to verify.");

        if (workup.PostDatPositive && string.IsNullOrWhiteSpace(elutionResult))
            return RuleResult.HardStop(Code, "The post-transfusion DAT is positive; record the elution result or document why elution was not performed.");

        return RuleResult.Pass(Code);
    }

    /// <summary>
    /// Every workup step still due, in the order of <see cref="Evaluate(bool, bool, WorkupState, string?)"/> (end-user
    /// testing pass 59: the page named one step at a time, so the work came out one save at a time). Empty when the
    /// gate passes.
    /// </summary>
    public static IReadOnlyList<string> Outstanding(
        bool clericalCheckCompleted,
        bool visualInspectionCompleted,
        WorkupState workup,
        string? elutionResult)
    {
        ArgumentNullException.ThrowIfNull(workup);
        var steps = new List<string>();
        if (!clericalCheckCompleted)
            steps.Add("Record the clerical check (patient ID, unit number, ABO/Rh).");
        if (!visualInspectionCompleted)
            steps.Add("Record the visual inspection of the returned unit or segments.");
        if (!workup.PreLinked)
            steps.Add("Choose the pre-transfusion specimen to order its workup tests.");
        if (!workup.PostLinked)
            steps.Add("Accession or choose the post-transfusion specimen to order its workup tests.");
        var pending = workup.PrePending + workup.PostPending;
        if (pending > 0)
            steps.Add($"Result and verify the workup tests: {pending} still to verify.");
        if (workup.PostDatPositive && string.IsNullOrWhiteSpace(elutionResult))
            steps.Add("The post-transfusion DAT is positive: record the elution result or why elution was not performed.");
        return steps;
    }

    /// <summary>The earlier checklist gate (typed DAT), kept for cases recorded before the specimen workup.</summary>
    public static RuleResult Evaluate(
        bool clericalCheckCompleted,
        bool visualInspectionCompleted,
        DatWorkupResult datResult,
        string? elutionResult)
    {
        if (!clericalCheckCompleted)
            return RuleResult.HardStop(Code, "Clerical check (patient ID, unit number, ABO/Rh) has not been recorded.");

        if (!visualInspectionCompleted)
            return RuleResult.HardStop(Code, "Visual inspection of the returned unit or segments has not been recorded.");

        if (datResult == DatWorkupResult.NotRecorded)
            return RuleResult.HardStop(Code, "Post-transfusion DAT result has not been recorded.");

        if (datResult == DatWorkupResult.Positive && string.IsNullOrWhiteSpace(elutionResult))
            return RuleResult.HardStop(Code, "DAT is positive; record the elution result or document why elution was not performed.");

        return RuleResult.Pass(Code);
    }
}
