using BloodBankLIS.Application.Issuing;

namespace BloodBankLIS.Application.Tests;

// TEST-BB-1284 (end-user testing pass 258): the Issued units board says in words where a unit stands against the
// return-to-available window.
public class TimeOutBoardTextTests
{
    [Fact]
    public void BoardText_SaysWhatTheWindowMeans()
    {
        Assert.Equal("No return time limit (exempt storage)", ReadyToIssueFormCue.TimeOutBoardText(false, false));
        Assert.Equal("Within the return window", ReadyToIssueFormCue.TimeOutBoardText(true, true));
        Assert.Equal("Return window passed: not returnable to stock", ReadyToIssueFormCue.TimeOutBoardText(true, false));
    }
}
