using BloodBankLIS.Web.Services;

namespace BloodBankLIS.Help.Tests;

public class MessageRawTextTests
{
    // TEST-BB-1225: HL7 and ASTM messages are shown one segment or record per line; their carriage returns become line
    // breaks (end-user testing pass 203).
    [Fact]
    public void ForDisplay_PutsEachSegmentOnItsOwnLine()
    {
        Assert.Equal("MSH|^~&|EHR\nPID|1||MRN0001\nORC|NW", MessageRawText.ForDisplay("MSH|^~&|EHR\rPID|1||MRN0001\rORC|NW\r"));
        Assert.Equal("H|^&\nP|1\nL|1|N", MessageRawText.ForDisplay("H|^&\r\nP|1\rL|1|N\r"));
        Assert.Equal(string.Empty, MessageRawText.ForDisplay(null));
    }
}
