using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class StatusHistoryLocationCueTests
{
    [Fact]
    public void Format_SamePlace_DoesNotSayItMoved()
    {
        var names = new Dictionary<long, string?> { [12] = "Paperwork fridge" };

        Assert.Null(StatusHistoryLocationCue.Format(null, null, names));
        Assert.Null(StatusHistoryLocationCue.Format(12, 12, names));
    }

    [Fact]
    public void Format_NamedMove_UsesTheNamesNotTheCodes()
    {
        var names = new Dictionary<long, string?>
        {
            [12] = "  Paperwork fridge  ",
            [18] = "Outdate fridge"
        };

        Assert.Equal(
            "from Paperwork fridge to Outdate fridge",
            StatusHistoryLocationCue.Format(12, 18, names));
        Assert.Equal("to Paperwork fridge", StatusHistoryLocationCue.Format(null, 12, names));
        Assert.Equal("from Paperwork fridge", StatusHistoryLocationCue.Format(12, null, names));
    }

    [Fact]
    public void Format_BlankOrUnknownName_KeepsTheLocationId()
    {
        var names = new Dictionary<long, string?> { [12] = "   " };

        Assert.Equal("from Location 12 to Location 18", StatusHistoryLocationCue.Format(12, 18, names));
        Assert.Equal("to Location 4", StatusHistoryLocationCue.Format(null, 4, namesById: null));
    }
}
