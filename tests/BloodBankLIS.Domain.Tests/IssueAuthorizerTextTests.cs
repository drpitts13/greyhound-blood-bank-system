using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

// TEST-BB-1274 (end-user testing pass 250): an issue's recorded override authorizer is shown; otherwise a
// massive-transfusion issue names the physician who called its protocol; otherwise nothing.
public class IssueAuthorizerTextTests
{
    [Fact]
    public void OverrideAuthorizer_ThenMtpCaller_ThenNothing()
    {
        Assert.Equal("Dr. Lee", IssueAuthorizerText.For(" Dr. Lee ", 1, "Dr. Demo Trauma"));
        Assert.Equal("Dr. Demo Trauma (called MTP #1)", IssueAuthorizerText.For(null, 1, "Dr. Demo Trauma"));
        Assert.Null(IssueAuthorizerText.For(" ", null, "Dr. Demo Trauma"));
        Assert.Null(IssueAuthorizerText.For(null, 1, " "));
    }
}
