using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

// TEST-BB-572: a dismissed pair stays off the report while every reason it is listed for was reviewed; a new reason
// brings it back.
public class PossibleDuplicateDismissalTests
{
    [Fact]
    public void StillDismissed_OnlyWhileNoNewReasonAppears()
    {
        var name = PossibleDuplicatePatientRule.SameNameAndBirthday;
        var shared = PossibleDuplicatePatientRule.SharedIdentifier("EPI-1", "EPI PI");

        Assert.True(PossibleDuplicatePatientRule.StillDismissed(name, name));
        Assert.True(PossibleDuplicatePatientRule.StillDismissed(name, $"{name}; {shared}"));
        Assert.False(PossibleDuplicatePatientRule.StillDismissed($"{name}; {shared}", name));
        Assert.False(PossibleDuplicatePatientRule.StillDismissed(shared, name));
    }
}
