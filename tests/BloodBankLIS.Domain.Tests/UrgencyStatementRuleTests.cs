using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class UrgencyStatementRuleTests
{
    private static readonly DateTime Now = new(2026, 11, 15, 12, 0, 0, DateTimeKind.Utc);

    // TEST-BB-1134: the statement is outstanding only for a release before testing was complete and until received; it
    // needs the signer and a received time neither in the future nor before the issue (end-user testing pass 117).
    [Fact]
    public void Outstanding_AndValidate()
    {
        Assert.True(UrgencyStatementRule.Outstanding(true, null));
        Assert.False(UrgencyStatementRule.Outstanding(true, Now));
        Assert.False(UrgencyStatementRule.Outstanding(false, null));

        var issued = Now.AddHours(-3);
        Assert.Null(UrgencyStatementRule.Validate(true, null, "Dr. Lee", Now, issued, Now));
        Assert.NotNull(UrgencyStatementRule.Validate(false, null, "Dr. Lee", Now, issued, Now));
        Assert.NotNull(UrgencyStatementRule.Validate(true, Now, "Dr. Lee", Now, issued, Now));
        Assert.NotNull(UrgencyStatementRule.Validate(true, null, "", Now, issued, Now));
        Assert.NotNull(UrgencyStatementRule.Validate(true, null, "Dr. Lee", Now.AddDays(1), issued, Now));
        Assert.NotNull(UrgencyStatementRule.Validate(true, null, "Dr. Lee", issued.AddDays(-2), issued, Now));
    }
}
