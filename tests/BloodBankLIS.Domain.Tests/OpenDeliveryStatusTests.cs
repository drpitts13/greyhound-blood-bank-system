using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class OpenDeliveryStatusTests
{
    // TEST-BB-1164: an open delivery says how many units are still to enter, or that all are entered (or more than
    // expected) and it is ready to review and complete (end-user testing pass 142).
    [Fact]
    public void Text_SaysWhatIsLeft_OrThatItIsReadyToComplete()
    {
        Assert.Equal("1 of 2 still to enter", OpenDeliveryStatus.Text(2, 1));
        Assert.False(OpenDeliveryStatus.ReadyToComplete(2, 1));
        Assert.Equal("All entered: review and complete", OpenDeliveryStatus.Text(2, 2));
        Assert.True(OpenDeliveryStatus.ReadyToComplete(2, 2));
        Assert.Equal("1 more than expected: review and complete with a note", OpenDeliveryStatus.Text(2, 3));
        Assert.True(OpenDeliveryStatus.ReadyToComplete(2, 3));
        Assert.Equal("0 entered; no expected count", OpenDeliveryStatus.Text(0, 0));
        Assert.False(OpenDeliveryStatus.ReadyToComplete(0, 4));
    }
}
