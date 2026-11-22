using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class TransfusionReportNoteTests
{
    // TEST-BB-943 (Cycle 701): an ordinary transfusion has no note; a reported one names the unit's status and whether it
    // was reviewed, by whom and what was found.
    [Fact]
    public void For_ReadsTheReportAndItsReview()
    {
        Assert.Null(TransfusionReportNote.For(null, null, null));
        Assert.Equal("Reported by the EHR while the unit was Quarantine in the blood bank; review pending on the supervisor queue",
            TransfusionReportNote.For(UnitStatus.Quarantine, null, null));
        Assert.Equal("Reported by the EHR while the unit was Recalled in the blood bank; reviewed by sup1: Physician informed",
            TransfusionReportNote.For(UnitStatus.Recalled, " sup1 ", "Physician informed "));
        Assert.Equal("Reported by the EHR while the unit was Damaged in the blood bank; reviewed by sup1",
            TransfusionReportNote.For(UnitStatus.Damaged, "sup1", null));
    }
}
