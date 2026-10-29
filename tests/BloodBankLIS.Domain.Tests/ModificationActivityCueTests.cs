using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class ModificationActivityCueTests
{
    [Fact]
    public void Format_KnownPatient_NamesThem()
    {
        Assert.Equal(
            "Irradiate Completed for Doe, Jane (MRN0001)",
            ModificationActivityCue.Format(" Irradiate ", "Completed", " Doe, Jane (MRN0001) "));
    }

    [Fact]
    public void Format_MissingOrder_DoesNotInventAPatient()
    {
        Assert.Equal(
            "Irradiate Completed. The order could not be read.",
            ModificationActivityCue.Format("Irradiate", "Completed", null));
        Assert.Equal(
            "Modification. The order could not be read.",
            ModificationActivityCue.Format("   ", "  ", "  "));
    }
}
