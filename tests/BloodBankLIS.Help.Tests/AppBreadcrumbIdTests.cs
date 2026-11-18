using BloodBankLIS.Web.Services;

namespace BloodBankLIS.Help.Tests;

public class AppBreadcrumbIdTests
{
    // TEST-BB-1248: a page under an id names what it is, not the bare id (end-user testing pass 226: a report read
    // "Dashboard / 17").
    [Theory]
    [InlineData("reports/17", "Dashboard|Reports|Run report")]
    [InlineData("reports", "Dashboard|Reports")]
    [InlineData("mtp/pack-records/5", "Dashboard|Pack Records #5")]
    [InlineData("admin/reports/design/3", "Dashboard|Administration|Reports|Design|#3")]
    public void IdPages_NameWhatTheyAre(string uri, string expected) =>
        Assert.Equal(expected, string.Join('|', AppBreadcrumbTrail.FromRelative(uri).Select(c => c.Label)));
}
