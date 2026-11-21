using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Web.Services;

namespace BloodBankLIS.Help.Tests;

// TEST-BB-1300 (end-user testing pass 274): an antigen result on the unit page, the chart's antigen profile and the
// antibody identification panel reads in words ("Not tested", not "NotTested").
public class AntigenResultTextTests
{
    [Fact]
    public void AntigenResult_ReadsInWords()
    {
        Assert.Equal("Not tested", Ui.EnumText(AntigenResult.NotTested));
        Assert.Equal("Negative", Ui.EnumText(AntigenResult.Negative));
    }
}
