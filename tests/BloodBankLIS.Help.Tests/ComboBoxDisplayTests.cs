using BloodBankLIS.Web.Components.Shared;

namespace BloodBankLIS.Help.Tests;

// TEST-BB-312: a combo box value set before its list loads shows the item once it arrives, never a bare internal id.
public class ComboBoxDisplayTests
{
    private sealed record Unit(long Id, string Number);

    private static ComboBoxDisplay.Result Resolve(long? value, IEnumerable<Unit> items, Func<long?, string>? unresolved = null) =>
        ComboBoxDisplay.Resolve(value, items, u => (long?)u.Id, u => u.Number, unresolved);

    [Fact]
    public void ValueInTheList_ShowsItsDisplayText()
    {
        var r = Resolve(1, [new Unit(1, "W0001230000001"), new Unit(2, "W0001230000002")]);

        Assert.Equal(new ComboBoxDisplay.Result("W0001230000001", true), r);
    }

    [Fact]
    public void ValueBeforeTheListLoads_IsUnresolved_AndUsesTheUnresolvedText()
    {
        var r = Resolve(1, [], id => $"Loading unit {id}");

        Assert.False(r.Resolved);
        Assert.Equal("Loading unit 1", r.Text);
    }

    [Fact]
    public void WithoutUnresolvedText_FallsBackToTheValue_AsBefore()
    {
        Assert.Equal(new ComboBoxDisplay.Result("7", false), Resolve(7, [new Unit(1, "W1")]));
    }

    [Fact]
    public void NullValue_IsEmptyAndResolved()
    {
        Assert.Equal(new ComboBoxDisplay.Result(string.Empty, true), Resolve(null, [new Unit(1, "W1")]));
    }

    [Fact]
    public void CustomStringValue_IsShownAsTyped()
    {
        var r = ComboBoxDisplay.Resolve<string, string>("free text", ["a", "b"], s => s, s => s.ToUpperInvariant(), customStringValue: true);

        Assert.Equal(new ComboBoxDisplay.Result("free text", true), r);
    }

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, true)]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    public void ShouldResolve_UntilTheItemIsFound(bool valueChanged, bool lastResolved, bool expected) =>
        Assert.Equal(expected, ComboBoxDisplay.ShouldResolve(valueChanged, lastResolved));

    [Fact]
    public void LoadSequence_ResolvesOnceTheItemArrives()
    {
        var first = Resolve(1, []);
        Assert.False(first.Resolved);
        Assert.True(ComboBoxDisplay.ShouldResolve(valueChanged: false, first.Resolved));

        var second = Resolve(1, [new Unit(1, "W0001230000001")]);
        Assert.Equal(new ComboBoxDisplay.Result("W0001230000001", true), second);
        Assert.False(ComboBoxDisplay.ShouldResolve(valueChanged: false, second.Resolved));
    }
}
