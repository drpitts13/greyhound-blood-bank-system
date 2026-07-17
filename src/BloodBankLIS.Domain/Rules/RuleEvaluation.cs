namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Aggregates many <see cref="RuleResult"/> values into a single decision.
/// Any HardStop makes the whole evaluation a HardStop regardless of other
/// results. One or more Warnings (and no HardStop) is overridable.
/// </summary>
public sealed class RuleEvaluation
{
    private readonly List<RuleResult> _results;

    public RuleEvaluation(IEnumerable<RuleResult> results)
    {
        _results = results?.ToList() ?? throw new ArgumentNullException(nameof(results));
    }

    public IReadOnlyList<RuleResult> Results => _results;

    public IReadOnlyList<RuleResult> HardStops =>
        _results.Where(r => r.Severity == RuleSeverity.HardStop).ToList();

    public IReadOnlyList<RuleResult> Warnings =>
        _results.Where(r => r.Severity == RuleSeverity.Warning).ToList();

    public RuleSeverity OverallSeverity
    {
        get
        {
            if (_results.Any(r => r.Severity == RuleSeverity.HardStop))
            {
                return RuleSeverity.HardStop;
            }

            return _results.Any(r => r.Severity == RuleSeverity.Warning)
                ? RuleSeverity.Warning
                : RuleSeverity.Pass;
        }
    }

    /// <summary>True when nothing objects: the action may proceed without override.</summary>
    public bool IsAllowed => OverallSeverity == RuleSeverity.Pass;

    /// <summary>
    /// True when the action is blocked but the only objections are Warnings, so an
    /// authorized override (reason + e-signature + audit) may permit it.
    /// </summary>
    public bool RequiresOverride => OverallSeverity == RuleSeverity.Warning;

    /// <summary>True when the action is hard-blocked and cannot be overridden.</summary>
    public bool IsHardStopped => OverallSeverity == RuleSeverity.HardStop;
}
