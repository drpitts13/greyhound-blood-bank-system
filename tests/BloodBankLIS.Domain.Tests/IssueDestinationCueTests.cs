using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class IssueDestinationCueTests
{
    private static StorageLocationLabel.CatalogLocation Place(long id, string? name, string? code) =>
        new(id, name, code);

    [Fact]
    public void Format_KnownCode_UsesTheName()
    {
        var catalog = new[] { Place(4, " Issue fridge ", " FR1 ") };
        Assert.Equal("Issue fridge", IssueDestinationCue.Format(" fr1 ", catalog));
        Assert.DoesNotContain("FR1", IssueDestinationCue.Format("FR1", catalog), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Format_BlankName_StaysTheLocationId()
    {
        var catalog = new[] { Place(9, "   ", "FR2") };
        var label = IssueDestinationCue.Format("FR2", catalog);
        Assert.Equal("Location 9", label);
        Assert.DoesNotContain("FR2", label, StringComparison.Ordinal);
    }

    [Fact]
    public void Format_OtherText_StaysAsTyped()
    {
        var catalog = new[]
        {
            Place(1, "OR fridge", "OR1"),
            Place(2, "OR1", "BAY")
        };
        Assert.Null(IssueDestinationCue.Format("   ", catalog));
        Assert.Equal("4W Med/Surg", IssueDestinationCue.Format(" 4W Med/Surg ", catalog));
        Assert.Equal("OR1", IssueDestinationCue.Format("OR1", catalog));
        Assert.Equal("FR1", IssueDestinationCue.Format("FR1", null));
        Assert.Equal("FR1", IssueDestinationCue.Format("FR1", Array.Empty<StorageLocationLabel.CatalogLocation>()));
    }
}
