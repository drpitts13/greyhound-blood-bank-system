using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class BenchModificationActivityCueTests
{
    [Fact]
    public void Format_NoOrder_SaysNoneWasRecorded()
    {
        Assert.Equal(
            "Wash (Source) — Clinical need. No patient order was recorded.",
            BenchModificationActivityCue.Format(" Wash ", " Source ", " Clinical need ", patientOrderRecorded: false));
    }

    [Fact]
    public void Format_BlankReason_DoesNotInventOne()
    {
        Assert.Equal(
            "Irradiate (Result). No patient order was recorded.",
            BenchModificationActivityCue.Format("Irradiate", "Result", "   ", patientOrderRecorded: false));
        Assert.Equal(
            "Modification. No patient order was recorded.",
            BenchModificationActivityCue.Format("  ", "  ", null, patientOrderRecorded: false));
    }

    [Fact]
    public void Format_OrderRecorded_DoesNotClaimThereWasNone()
    {
        Assert.Equal(
            "Pool (Source) — For the order",
            BenchModificationActivityCue.Format("Pool", "Source", "For the order", patientOrderRecorded: true));
    }
}
