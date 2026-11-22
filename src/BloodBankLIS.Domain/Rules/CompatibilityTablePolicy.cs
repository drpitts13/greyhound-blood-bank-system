using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Rules;

/// <summary>An active row of the compatibility table in use, as the issue gate reads it.</summary>
public sealed record CompatibilityTableRow(string RuleCode, ComponentClass ComponentClass, string Severity, bool IsActive);

/// <summary>
/// OCD-052 (decided 2026-11-03): the issue gate reads the active compatibility table. A row for the
/// gate's rule code and the unit's component class can make that rule stricter (a warning becomes a hard
/// stop), never weaker: a hard stop stays a hard stop, and a missing or deactivated row leaves the
/// built-in result as it is. A row's code is the gate's code, or that code with a component suffix
/// (for example ISS-ABO-COMPAT.PLT).
/// The seeded table copies <see cref="CompatibilityRuleCatalog.Defaults"/>, which documents today's built-in
/// behavior; some defaults read HardStop for rules the gate lets a supervisor override (antigen-negative,
/// antibody-history crossmatch). So a row escalates only when it is stricter than its built-in default:
/// the table as seeded changes nothing at issue.
/// </summary>
public static class CompatibilityTablePolicy
{
    public const string SofterCode = "COMPAT.SEVERITY.SOFTER";

    public const string EscalatedNote = " The active compatibility table makes this a hard stop.";

    public static bool Matches(CompatibilityTableRow row, string resultCode, ComponentClass componentClass) =>
        row.IsActive
        && row.ComponentClass == componentClass
        && (string.Equals(row.RuleCode, resultCode, StringComparison.OrdinalIgnoreCase)
            || row.RuleCode.StartsWith(resultCode + ".", StringComparison.OrdinalIgnoreCase));

    public static RuleEvaluation Apply(
        RuleEvaluation evaluation,
        ComponentClass componentClass,
        IReadOnlyCollection<CompatibilityTableRow> activeTable)
    {
        ArgumentNullException.ThrowIfNull(evaluation);
        if (activeTable.Count == 0)
        {
            return evaluation;
        }

        return new RuleEvaluation(evaluation.Results.Select(result =>
            result.Severity == RuleSeverity.Warning
            && activeTable.Any(row => Matches(row, result.Code, componentClass) && RaisesAboveBuiltIn(row))
                ? RuleResult.HardStop(result.Code, result.Message + EscalatedNote)
                : result));
    }

    /// <summary>
    /// A row that would make a built-in rule weaker than its default is refused at save: the gate would not
    /// apply it, so the table must not say it.
    /// </summary>
    public static RuleResult EvaluateRowSeverity(string ruleCode, string severity)
    {
        var builtIn = CompatibilityRuleCatalog.Find(ruleCode);
        if (builtIn is null || Rank(severity) >= Rank(builtIn.Severity))
        {
            return RuleResult.Pass(SofterCode);
        }

        return RuleResult.HardStop(
            SofterCode,
            $"{builtIn.RuleCode} is a {builtIn.Severity} by default. The issue gate makes rules stricter from this table, never weaker, so {severity} cannot be saved here.");
    }

    public const string AtIssueRaises = "Raises to hard stop";
    public const string AtIssueRaisesIfReported = "Hard stop if the gate reports this code";
    public const string AtIssueSame = "Same as built-in";
    public const string AtIssueNoEffect = "No effect at issue";
    public const string AtIssueOff = "Off – built-in rule applies";

    /// <summary>What this row does at issue, for the admin table: the same decision <see cref="Apply"/> makes.</summary>
    public static string DescribeAtIssue(CompatibilityTableRow row)
    {
        if (!row.IsActive)
        {
            return AtIssueOff;
        }

        var builtIn = CompatibilityRuleCatalog.Find(row.RuleCode);
        if (RaisesAboveBuiltIn(row))
        {
            // Only catalog codes are known to come from the gate; any other code applies only if a gate rule reports it.
            return builtIn is null ? AtIssueRaisesIfReported : AtIssueRaises;
        }

        return builtIn is null ? AtIssueNoEffect : AtIssueSame;
    }

    /// <summary>A HardStop row raises a rule only when its built-in default is not already HardStop.</summary>
    public static bool RaisesAboveBuiltIn(CompatibilityTableRow row) =>
        IsHardStop(row.Severity) && !IsHardStop(CompatibilityRuleCatalog.Find(row.RuleCode)?.Severity ?? string.Empty);

    private static bool IsHardStop(string severity) =>
        string.Equals(severity, "HardStop", StringComparison.OrdinalIgnoreCase);

    private static int Rank(string severity) => severity?.Trim().ToLowerInvariant() switch
    {
        "hardstop" => 2,
        "warning" => 1,
        _ => 0
    };
}
