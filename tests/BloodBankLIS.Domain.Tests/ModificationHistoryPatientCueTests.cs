using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class ModificationHistoryPatientCueTests
{
    [Fact]
    public void Format_KnownPatient_NamesThem()
    {
        Assert.Equal(
            "Poe, Ann (MRN0001)",
            ModificationHistoryPatientCue.Format(" Poe, Ann (MRN0001) "));
    }

    [Fact]
    public void Format_NoOrder_SaysNoneWasRecorded()
    {
        Assert.Equal(
            "No patient order was recorded.",
            ModificationHistoryPatientCue.Format("  "));
    }

    [Fact]
    public void Format_MissingOrder_DoesNotInventAPatient()
    {
        Assert.Equal(
            "The order could not be read.",
            ModificationHistoryPatientCue.Format(null, orderCouldNotBeRead: true));
    }

    [Fact]
    public void Format_DisagreeingOrders_DoesNotPickOne()
    {
        Assert.Equal(
            "The linked orders do not name one patient.",
            ModificationHistoryPatientCue.Format("Poe, Ann (MRN0001)", linkedOrdersDisagree: true));
    }
}
