using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// OCD-048 (decided by the product owner, 2026-11-02): a hand-recorded ABO/Rh counts as an ABO/Rh determination only
/// when it is documented from a previous blood bank system or a documented outside source. Undocumented, it can
/// still be recorded (it is the current type), but it never satisfies the two-determination rule.
/// </summary>
public static class ManualBloodTypeDocumentationRule
{
    public const string Code = "BT-MANUAL-SOURCE";

    /// <summary>A documented source must name the record or report it came from.</summary>
    public static RuleResult Evaluate(ManualTypeDocumentation documentation, string? reference) =>
        documentation != ManualTypeDocumentation.NotDocumented && string.IsNullOrWhiteSpace(reference)
            ? RuleResult.HardStop(Code,
                "Name the record the type came from: the previous blood bank system's record, or the outside report (facility, date, reference).")
            : RuleResult.Pass(Code);

    /// <summary>Whether a stored type is a determination for the two-determination rule.</summary>
    public static bool CountsAsDetermination(BloodTypeSource source, ManualTypeDocumentation? documentation) =>
        source != BloodTypeSource.ManualEntry
        || documentation is ManualTypeDocumentation.PreviousBloodBankSystem or ManualTypeDocumentation.OutsideSource;

    public static string Describe(ManualTypeDocumentation? documentation) => documentation switch
    {
        ManualTypeDocumentation.PreviousBloodBankSystem => "from a previous blood bank system",
        ManualTypeDocumentation.OutsideSource => "from a documented outside source",
        _ => "not documented; does not count as a determination"
    };
}
