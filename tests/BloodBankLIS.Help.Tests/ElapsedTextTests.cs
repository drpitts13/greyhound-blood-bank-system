using BloodBankLIS.Web.Services;

namespace BloodBankLIS.Help.Tests;

public class ElapsedTextTests
{
    // TEST-BB-1217: how long an MTP has been open reads at a glance (end-user testing pass 195: "15487 min").
    [Theory]
    [InlineData(45, "45 min")]
    [InlineData(130, "2 h 10 min")]
    [InlineData(180, "3 h")]
    [InlineData(15487, "10 d 18 h")]
    [InlineData(2880, "2 d")]
    public void FromMinutes_ReadsAtAGlance(int minutes, string expected) =>
        Assert.Equal(expected, ElapsedText.FromMinutes(minutes));
}
