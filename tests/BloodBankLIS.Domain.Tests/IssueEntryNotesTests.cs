using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

/// <summary>How an issue was entered, in plain words for the product history (Cycle 676).</summary>
public class IssueEntryNotesTests
{
    // TEST-BB-882: a late entry gives its reason; a downtime back-entry says so and gives its reason on its own line; a
    // reason for a time outside every outage and the neonatal no-crossmatch issue are each said; an ordinary issue has none.
    [Fact]
    public void Notes_SayHowTheIssueWasEntered()
    {
        Assert.Empty(IssueEntryNotes.For(null, false, null, false));
        Assert.Equal(["Late entry: Charted after the shift"], IssueEntryNotes.For(" Charted after the shift ", false, null, false));
        Assert.Equal(
            ["Entered from a paper issue record after downtime", "Reason: Issued on paper during downtime #2: LIS upgrade", "Outside every recorded outage: Unrecorded outage"],
            IssueEntryNotes.For("Issued on paper during downtime #2: LIS upgrade", true, "Unrecorded outage", false));
        Assert.Equal(["Infant, negative screen: group O issued without a crossmatch (neonatal rules)"], IssueEntryNotes.For(null, false, " ", true));
    }
}
