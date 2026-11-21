using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class PatientTypingTestRuleTests
{
    // TEST-BB-1144: ABORH, or an ABO/Rh test contributing to the ABO/Rh history, types the patient; an ABO/Rh test that
    // does not contribute, or another test that does, does not (end-user testing pass 123).
    [Theory]
    [InlineData("ABORH", null, false, true)]
    [InlineData("aborh", ResultValueType.Coded, false, true)]
    [InlineData("TSNEO", ResultValueType.AboRh, true, true)]
    [InlineData("REFTYPE", ResultValueType.AboRh, false, false)]
    [InlineData("WEAKD", ResultValueType.Coded, true, false)]
    public void Is(string code, ResultValueType? type, bool contributes, bool expected) =>
        Assert.Equal(expected, PatientTypingTestRule.Is(code, type, contributes));
}
