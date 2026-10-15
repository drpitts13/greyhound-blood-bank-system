using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class BenchModificationActivityCueTests
{
    [Fact]
    public void Format_NoOrder_SaysNoneWasRecorded()
    {
        Assert.Equal(
            "Wash (Source) — Clinical need. No patient order was recorded.",
            BenchModificationActivityCue.Format(" Wash ", " Source ", " Clinical need "));
    }

    [Fact]
    public void Format_BlankReason_DoesNotInventOne()
    {
        Assert.Equal(
            "Irradiate (Result). No patient order was recorded.",
            BenchModificationActivityCue.Format("Irradiate", "Result", "   "));
        Assert.Equal(
            "Modification. No patient order was recorded.",
            BenchModificationActivityCue.Format("  ", "  ", null));
    }

    [Fact]
    public void Format_KnownPatient_NamesThem()
    {
        Assert.Equal(
            "Pool (Source) — For the order for Poe, Ann (MRN0001)",
            BenchModificationActivityCue.Format("Pool", "Source", "For the order", " Poe, Ann (MRN0001) "));
        Assert.Equal(
            "Thaw (Result) for patient #4 (MRN2)",
            BenchModificationActivityCue.Format("Thaw", "Result", "  ", "patient #4 (MRN2)"));
    }

    [Fact]
    public void Format_MissingOrder_DoesNotInventAPatient()
    {
        Assert.Equal(
            "Pool (Source) — For the order. The order could not be read.",
            BenchModificationActivityCue.Format("Pool", "Source", "For the order", orderCouldNotBeRead: true));
    }

    [Fact]
    public void Format_DisagreeingOrders_DoesNotPickOne()
    {
        Assert.Equal(
            "Pool (Source) — For the order. The linked orders do not name one patient.",
            BenchModificationActivityCue.Format(
                "Pool",
                "Source",
                "For the order",
                "Poe, Ann (MRN0001)",
                linkedOrdersDisagree: true));
    }
}
