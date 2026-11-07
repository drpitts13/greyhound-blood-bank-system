using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

// TEST-BB-324: a massive transfusion protocol is called by a named clinician for a reason, once per patient, and
// every massive-transfusion issue belongs to it.
public class MtpActivationRuleTests
{
    [Fact]
    public void Activate_WithClinicianAndReason_Passes() =>
        Assert.False(MtpActivationRule.EvaluateActivate("Dr. Rivera", "Hemorrhagic shock", false, PatientStatus.Active).IsHardStopped);

    [Theory]
    [InlineData(null, "Hemorrhagic shock")]
    [InlineData("  ", "Hemorrhagic shock")]
    [InlineData("Dr. Rivera", null)]
    [InlineData("Dr. Rivera", " ")]
    public void Activate_WithoutClinicianOrReason_IsHardStopped(string? requestedBy, string? reason)
    {
        var evaluation = MtpActivationRule.EvaluateActivate(requestedBy, reason, false, PatientStatus.Active);
        Assert.Contains(evaluation.HardStops, r => r.Code == MtpActivationRule.ActivateCode);
    }

    [Fact]
    public void Activate_WhenAlreadyActive_IsHardStopped()
    {
        var evaluation = MtpActivationRule.EvaluateActivate("Dr. Rivera", "Shock", true, PatientStatus.Active);
        Assert.Contains(evaluation.HardStops, r => r.Code == MtpActivationRule.AlreadyActiveCode);
    }

    [Fact]
    public void Activate_OnMergedChart_IsHardStopped() =>
        Assert.True(MtpActivationRule.EvaluateActivate("Dr. Rivera", "Shock", false, PatientStatus.Merged).IsHardStopped);

    [Theory]
    [InlineData(null, MtpActivationStatus.Active, true)]
    [InlineData("Bleeding controlled", MtpActivationStatus.Closed, true)]
    [InlineData("Bleeding controlled", MtpActivationStatus.Active, false)]
    public void Close_NeedsReasonAndAnActiveProtocol(string? reason, MtpActivationStatus status, bool stopped) =>
        Assert.Equal(stopped, MtpActivationRule.EvaluateClose(reason, status).IsHardStopped);

    [Theory]
    [InlineData(IssueType.MassiveTransfusion, false, true)]
    [InlineData(IssueType.MassiveTransfusion, true, false)]
    [InlineData(IssueType.EmergencyRelease, false, false)]
    [InlineData(IssueType.Standard, false, false)]
    public void Issue_UnderMtp_NeedsAnActiveActivation(IssueType type, bool active, bool stopped) =>
        Assert.Equal(stopped, MtpActivationRule.EvaluateIssue(type, active).Severity == RuleSeverity.HardStop);
}
