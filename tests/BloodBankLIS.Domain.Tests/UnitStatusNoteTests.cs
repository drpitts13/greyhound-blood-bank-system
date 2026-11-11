using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class UnitStatusNoteTests
{
    // TEST-BB-930: each out-of-use status names its recorded reason (a shipped unit, where it went); a unit in routine
    // use, or with no reason recorded, has no note; a long reason is shortened.
    [Theory]
    [InlineData(UnitStatus.ShippedOut, "shipped to St. Mary's Hospital")]
    [InlineData(UnitStatus.Recalled, "recalled: Donor reactive")]
    [InlineData(UnitStatus.Quarantine, "quarantine: Clots seen")]
    [InlineData(UnitStatus.OnHold, "on hold: Paperwork")]
    [InlineData(UnitStatus.Missing, "missing: Not in fridge")]
    [InlineData(UnitStatus.Damaged, "damaged: Bag leak")]
    [InlineData(UnitStatus.Discarded, "discarded: Expired")]
    [InlineData(UnitStatus.ReturnedToSupplier, "returned to supplier: Recall R-1")]
    [InlineData(UnitStatus.Available, null)]
    [InlineData(UnitStatus.Issued, null)]
    public void For_NamesTheReason(UnitStatus status, string? expected)
    {
        Assert.Equal(expected, UnitStatusNote.For(status, "St. Mary's Hospital", "Donor reactive", "Clots seen", "Paperwork",
            "Not in fridge", "Bag leak", "Expired", "Recall R-1"));
    }

    [Fact]
    public void For_NoReason_OrALongOne()
    {
        Assert.Null(UnitStatusNote.For(UnitStatus.Recalled));
        var note = UnitStatusNote.For(UnitStatus.Recalled, recallReason: new string('x', 100))!;
        Assert.Equal("recalled: ".Length + UnitStatusNote.MaxReasonLength, note.Length);
        Assert.EndsWith("…", note);
    }
}
