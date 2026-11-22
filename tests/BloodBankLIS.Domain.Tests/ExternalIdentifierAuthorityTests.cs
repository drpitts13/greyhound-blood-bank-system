using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

// TEST-BB-570: an "also on" mark is read back as the issuer and the other chart's MRN; the same issuer is recognized
// with or without the mark.
public class ExternalIdentifierAuthorityTests
{
    [Fact]
    public void AlsoOnMark_ReadsBackTheIssuerAndTheOtherChart()
    {
        var marked = ExternalIdentifierAuthority.AlsoOn("EPI PI", "MRN-1");
        Assert.Equal("EPI PI, also on MRN-1", marked);
        Assert.Equal("EPI PI", ExternalIdentifierAuthority.Base(marked));
        Assert.Equal("MRN-1", ExternalIdentifierAuthority.OtherMrn(marked));
        Assert.Equal("also on MRN-1", ExternalIdentifierAuthority.AlsoOn(null, "MRN-1"));
        Assert.Null(ExternalIdentifierAuthority.Base("also on MRN-1"));
        Assert.Null(ExternalIdentifierAuthority.OtherMrn("EPI PI"));
    }

    [Fact]
    public void SameIssuer_IgnoresTheMark()
    {
        Assert.True(ExternalIdentifierAuthority.SameIssuer("EPI PI", "epi pi, also on MRN-1"));
        Assert.True(ExternalIdentifierAuthority.SameIssuer(null, "also on MRN-1"));
        Assert.False(ExternalIdentifierAuthority.SameIssuer("EPI PI", "HOSP MR"));
    }
}
