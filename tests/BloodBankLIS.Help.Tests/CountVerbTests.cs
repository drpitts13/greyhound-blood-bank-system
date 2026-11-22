using BloodBankLIS.Web.Services;

namespace BloodBankLIS.Help.Tests;

public class CountVerbTests
{
    // TEST-BB-1330 (end-user testing pass 303): a worklist count takes the verb that agrees with it ("1 needs
    // interpretation", "2 have an expired specimen").
    [Fact]
    public void For_AgreesWithTheCount()
    {
        Assert.Equal("needs", CountVerb.For(1, "needs", "need"));
        Assert.Equal("need", CountVerb.For(2, "needs", "need"));
        Assert.Equal("have", CountVerb.For(0, "has", "have"));
    }
}
