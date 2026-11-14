using BloodBankLIS.Application.Inventory;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Web.Services;

namespace BloodBankLIS.Help.Tests;

public class RetypeBoardScanTests
{
    private static ProductRetypeWorkItemDto Row(long id, string number) =>
        new(id, number, "RBC-LR", "Red Blood Cells", AboGroup.O, RhType.Positive, "O+", DateTime.UtcNow, UnitStatus.Received);

    // TEST-BB-721: a scanned DIN barcode or a typed unit number picks the pending unit to retype; a unit not waiting, an
    // unreadable barcode, and a divided number that names two rows each say so instead.
    [Fact]
    public void Scan_PicksThePendingUnit()
    {
        var pending = new[] { Row(1, "G123417654321"), Row(2, "W000123RET0002"), Row(5, "W000123RET0001"), Row(3, "W000123DIV0001"), Row(4, "w000123div0001") };

        Assert.Equal(1, RetypeBoardScan.Find("=G12341765432100", pending).Row?.UnitId);
        // A typed number is matched exactly before it is read as a DIN plus check character (found live: W000123RET0002
        // matched both RET0001 and RET0002 as "W000123RET000" plus one character).
        Assert.Equal(2, RetypeBoardScan.Find(" w000123ret0002 ", pending).Row?.UnitId);
        Assert.Equal(5, RetypeBoardScan.Find("W000123RET0001", pending).Row?.UnitId);

        var notPending = RetypeBoardScan.Find("W000123RET0099", pending);
        Assert.Null(notPending.Row);
        Assert.Equal("W000123RET0099 is not waiting for a retype. Open it from Search to see its status.", notPending.Message);

        Assert.Contains("not a readable unit number", RetypeBoardScan.Find("=G12", pending).Message);
        Assert.Contains("names 2 pending units", RetypeBoardScan.Find("W000123DIV0001", pending).Message);
        Assert.Equal(new RetypeBoardScan.Outcome(null, null), RetypeBoardScan.Find("  ", pending));
    }
}
