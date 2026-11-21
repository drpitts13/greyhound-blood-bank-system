using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Web.Services;

namespace BloodBankLIS.Help.Tests;

/// <summary>The Tests help explains every result value type (end-user testing loop 2 tick 37).</summary>
public class TestValueTypeHelpTests
{
    // TEST-BB-1413: each ResultValueType has a row in the Tests help's value type table, by the name the builder shows; a
    // new value type fails here until it is explained.
    [Fact]
    public void Help_ExplainsEveryValueType()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "BloodBankLIS.slnx")))
        {
            dir = dir.Parent;
        }

        var help = File.ReadAllText(Path.Combine(dir!.FullName, "docs", "user-manual", "admin", "tests.md"));
        Assert.All(Enum.GetValues<ResultValueType>(), t =>
            Assert.Contains($"| **{(t == ResultValueType.Subtest ? "Subtest" : Ui.EnumText(t))}** |", help, StringComparison.OrdinalIgnoreCase));
    }
}
