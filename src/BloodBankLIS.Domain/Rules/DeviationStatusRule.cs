using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// A deviation is closed only with its corrective action written down (which may say why none is needed), and a
/// corrective action once recorded is edited, never blanked: the quality record keeps what was done.
/// </summary>
public static class DeviationStatusRule
{
    public const string Code = "DEV-STATUS";

    public static RuleResult Evaluate(DeviationStatus to, string? recordedAction, string? newAction)
    {
        if (newAction is not null && string.IsNullOrWhiteSpace(newAction) && !string.IsNullOrWhiteSpace(recordedAction))
        {
            return RuleResult.HardStop(Code, "The corrective action already recorded cannot be cleared. Edit it instead.");
        }

        var action = newAction ?? recordedAction;
        if (to == DeviationStatus.Closed && string.IsNullOrWhiteSpace(action))
        {
            return RuleResult.HardStop(Code, "Record the corrective action, or why none is needed, before closing the deviation.");
        }

        return RuleResult.Pass(Code);
    }
}
