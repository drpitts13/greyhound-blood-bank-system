using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

// TEST-BB-564: an irradiation records the irradiator and the dose indicator; an indicator that did not confirm is
// refused; the irradiator and a confirmed indicator are required only under the facility policy (OCD-067).
public class ModificationDeviceRuleTests
{
    [Fact]
    public void Irradiation_RefusesAnUnconfirmedIndicator_EvenWithThePolicyOff()
    {
        var stop = ModificationDeviceRule.Evaluate(ModificationType.Irradiate, false, "IRR-1", indicatorConfirmed: false);
        Assert.Equal(RuleSeverity.HardStop, stop.Severity);
        Assert.Contains("not recorded as irradiated", stop.Message);
        Assert.Equal(RuleSeverity.Pass, ModificationDeviceRule.Evaluate(ModificationType.Irradiate, false, null, null).Severity);
    }

    [Fact]
    public void Irradiation_UnderThePolicy_NeedsTheIrradiatorAndAConfirmedIndicator()
    {
        Assert.Contains("irradiator", ModificationDeviceRule.Evaluate(ModificationType.Irradiate, true, " ", true).Message);
        Assert.Contains("dose indicator", ModificationDeviceRule.Evaluate(ModificationType.Irradiate, true, "IRR-1", null).Message);
        Assert.Equal(RuleSeverity.Pass, ModificationDeviceRule.Evaluate(ModificationType.Irradiate, true, "IRR-1", true).Severity);
    }

    [Fact]
    public void OtherModifications_RecordADeviceWithoutChecks()
    {
        Assert.Equal(RuleSeverity.Pass, ModificationDeviceRule.Evaluate(ModificationType.Wash, true, null, false).Severity);
        Assert.Equal("IRR-1, indicator confirmed", ModificationDeviceRule.Describe(" IRR-1 ", true));
        Assert.Equal("WASHER-2", ModificationDeviceRule.Describe("WASHER-2", null));
        Assert.Equal("indicator not confirmed", ModificationDeviceRule.Describe(null, false));
        Assert.Null(ModificationDeviceRule.Describe(" ", null));
    }
}
