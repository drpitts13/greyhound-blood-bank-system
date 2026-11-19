using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Web.Services;

namespace BloodBankLIS.Help.Tests;

public class CoolerCheckNoteTextTests
{
    // TEST-BB-1317 (end-user testing pass 290): a cooler check with differences says when, by whom and what it found, in
    // view under the badge; a check that found the cooler ready, or no check, says nothing.
    [Fact]
    public void For_WritesOutWhatTheCheckFound()
    {
        var at = new DateTime(2026, 11, 6, 14, 10, 0, DateTimeKind.Utc);
        Assert.Equal($"Checked {ClinicalTimeText.Local(at)} by tech2: 1 unit missing (W0001230009104)",
            CoolerCheckNoteText.For(at, false, " 1 unit missing (W0001230009104) ", "tech2"));
        Assert.Equal($"Checked {ClinicalTimeText.Local(at)}", CoolerCheckNoteText.For(at, null, null, null));
        Assert.Null(CoolerCheckNoteText.For(at, true, "Ready", "tech2"));
        Assert.Null(CoolerCheckNoteText.For(null, false, "x", "tech2"));
    }
}
