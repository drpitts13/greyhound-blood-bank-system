using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class ReturnActivityCueTests
{
    [Fact]
    public void Format_KnownPatient_NamesThemAndKeepsTheReason()
    {
        Assert.Equal(
            "Returned — Unused from Doe, Jane (MRN0001)",
            ReturnActivityCue.Format(false, " Unused ", "Doe, Jane (MRN0001)"));
        Assert.Equal(
            "Returned (reissue eligible) — Cooler intact from Doe, Jane (MRN0001)",
            ReturnActivityCue.Format(true, "Cooler intact", " Doe, Jane (MRN0001) "));
    }

    [Fact]
    public void Format_MissingIssue_DoesNotInventAPatient()
    {
        Assert.Equal(
            "Returned — Unused. The issuing record could not be read.",
            ReturnActivityCue.Format(false, "Unused", null));
        Assert.Equal(
            "Returned (reissue eligible). The issuing record could not be read.",
            ReturnActivityCue.Format(true, "   ", "  "));
    }
}
