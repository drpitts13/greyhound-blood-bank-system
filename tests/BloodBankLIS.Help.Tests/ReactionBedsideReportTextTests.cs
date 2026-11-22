using BloodBankLIS.Web.Services;

namespace BloodBankLIS.Help.Tests;

public class ReactionBedsideReportTextTests
{
    // TEST-BB-1316 (end-user testing pass 289): the investigation shows what the bedside reported (what was done and what
    // was seen), which was only on the printed reaction report; nothing reported shows nothing.
    [Fact]
    public void For_JoinsWhatWasDoneAndSeen()
    {
        Assert.Equal(
            "Transfusion stopped at 12 minutes. Temperature rose 1.8C with rigors. Observations: Febrile non-haemolytic reaction suspected; no visible haemoglobinuria.",
            ReactionBedsideReportText.For(
                " Transfusion stopped at 12 minutes. Temperature rose 1.8C with rigors. ",
                "Febrile non-haemolytic reaction suspected; no visible haemoglobinuria."));
        Assert.Equal("Observations: Hives on the forearms.", ReactionBedsideReportText.For(null, "Hives on the forearms"));
        Assert.Equal("Stopped.", ReactionBedsideReportText.For("Stopped", " "));
        Assert.Null(ReactionBedsideReportText.For(null, "  "));
    }
}
