using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// The kind of override recorded at issue, and how review screens name it. An MTP issue is recorded as a
/// massive-transfusion release, not as an ordinary warning override, so second-person review and reports
/// see it for what it was. Rows recorded before this distinction keep the action they were given.
/// </summary>
public static class OverrideActionRule
{
    public static OverrideAction ForIssue(IssueType issueType) => issueType switch
    {
        IssueType.EmergencyRelease => OverrideAction.EmergencyRelease,
        IssueType.MassiveTransfusion => OverrideAction.MassiveTransfusion,
        _ => OverrideAction.WarningOverride
    };

    public static string Describe(OverrideAction action) => action switch
    {
        OverrideAction.EmergencyRelease => "Emergency release",
        OverrideAction.MassiveTransfusion => "Massive transfusion release",
        _ => "Warning override"
    };
}
