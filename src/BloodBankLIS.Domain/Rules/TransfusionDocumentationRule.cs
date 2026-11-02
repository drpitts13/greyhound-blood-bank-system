using BloodBankLIS.Domain.Isbt128;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Consistency of a documented transfusion's times and volume. These are data checks, not clinical limits: a
/// transfusion cannot end before it starts or be recorded in the future, and more cannot be given than the bag held.
/// A small allowance covers bedside and server clocks that disagree.
/// </summary>
public static class TransfusionDocumentationRule
{
    public const string Code = "TXN-DOCUMENTATION";
    public static readonly TimeSpan ClockAllowance = TimeSpan.FromMinutes(5);

    public static RuleEvaluation Evaluate(DateTime? startUtc, DateTime? stopUtc, decimal? volume, decimal? unitVolume, DateTime nowUtc)
    {
        var results = new List<RuleResult>();
        var latest = nowUtc + ClockAllowance;
        if (startUtc is DateTime start && start > latest)
        {
            results.Add(RuleResult.HardStop(Code, "The start time is in the future. Enter when the transfusion actually started."));
        }

        if (stopUtc is DateTime stop && stop > latest)
        {
            results.Add(RuleResult.HardStop(Code, "The stop time is in the future. Enter when the transfusion actually stopped."));
        }

        if (startUtc is DateTime s && stopUtc is DateTime e && e < s)
        {
            results.Add(RuleResult.HardStop(Code, "The stop time is before the start time."));
        }

        if (volume is decimal v && v <= 0)
        {
            results.Add(RuleResult.HardStop(Code, "Volume transfused must be more than 0 mL, or left blank."));
        }
        else if (volume is decimal given && unitVolume is decimal bag && bag > 0 && given > bag)
        {
            results.Add(RuleResult.HardStop(Code, $"Volume transfused ({given:0.#} mL) is more than the unit held ({bag:0.#} mL)."));
        }

        if (results.Count == 0)
        {
            results.Add(RuleResult.Pass(Code));
        }

        return new RuleEvaluation(results);
    }

    /// <summary>
    /// The start time against the unit's own life: a transfusion cannot start before the unit was issued, and a unit
    /// is judged in date at the moment it was started, not when the record is entered. A unit that expired during or
    /// after its transfusion can still be documented; one that had expired before it was started cannot.
    /// </summary>
    public static RuleEvaluation EvaluateAgainstUnit(DateTime startUtc, DateTime issuedUtc, DateTime unitExpiresUtc)
    {
        var results = new List<RuleResult>();
        if (startUtc < issuedUtc - ClockAllowance)
        {
            results.Add(RuleResult.HardStop(Code,
                $"The start time ({startUtc:yyyy-MM-dd HH:mm} UTC) is before the unit was issued ({issuedUtc:yyyy-MM-dd HH:mm} UTC). Enter when the transfusion actually started."));
        }

        if (startUtc >= unitExpiresUtc)
        {
            results.Add(RuleResult.HardStop(IsbtErrorCodes.ComponentExpired,
                $"{IsbtErrorCodes.ComponentExpired}: The unit had expired ({unitExpiresUtc:yyyy-MM-dd HH:mm} UTC) before the transfusion started."));
        }

        if (results.Count == 0)
        {
            results.Add(RuleResult.Pass(Code));
        }

        return new RuleEvaluation(results);
    }
}
