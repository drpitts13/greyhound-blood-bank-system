namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Patient phrase for an order-linked modification on the unit timeline.
/// A missing order does not invent a patient. A blank action name stays "Modification".
/// </summary>
public static class ModificationActivityCue
{
    public static string Format(string? actionName, string? status, string? patientLabel)
    {
        var name = string.IsNullOrWhiteSpace(actionName) ? "Modification" : actionName.Trim();
        var state = string.IsNullOrWhiteSpace(status) ? null : status.Trim();
        var head = state is null ? name : $"{name} {state}";
        return string.IsNullOrWhiteSpace(patientLabel)
            ? $"{head}. The order could not be read."
            : $"{head} for {patientLabel.Trim()}";
    }
}
