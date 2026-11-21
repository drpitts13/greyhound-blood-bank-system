using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Help.Tests;

/// <summary>The Actions page and help name every modification an order may request (end-user testing loop 2 tick 35).</summary>
public class OrderActionWordingTests
{
    private static readonly Dictionary<ModificationType, string> Words = new()
    {
        [ModificationType.Irradiate] = "irradiate",
        [ModificationType.Wash] = "wash",
        [ModificationType.Thaw] = "thaw",
        [ModificationType.VolumeReduction] = "volume reduce",
        [ModificationType.Leukoreduction] = "leukoreduce",
        [ModificationType.Deglycerolize] = "deglycerolize",
        [ModificationType.PlasmaReduction] = "plasma reduce"
    };

    // TEST-BB-1411: each type OrderActionValidator accepts is named on Admin › Actions and in the two help pages (they
    // listed five after deglycerolize and plasma reduction were added); a newly attachable type fails here until it is named.
    [Theory]
    [InlineData("src/BloodBankLIS.Web/Components/Pages/Admin/AdminActions.razor")]
    [InlineData("docs/user-manual/clinical/modifications.md")]
    [InlineData("docs/user-manual/clinical/patient-orders.md")]
    public void EveryAttachableType_IsNamed(string relativePath)
    {
        var attachable = Enum.GetValues<ModificationType>().Where(t => OrderActionValidator.IsOrderAttachable(t)).ToList();
        Assert.All(attachable, t => Assert.True(Words.ContainsKey(t), $"Add wording for {t}."));

        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "BloodBankLIS.slnx")))
        {
            dir = dir.Parent;
        }

        var text = File.ReadAllText(Path.Combine(dir!.FullName, relativePath)).ToLowerInvariant();
        Assert.All(attachable, t => Assert.Contains(Words[t], text));
    }
}

/// <summary>Modify products names every modification type (end-user testing loop 2 tick 36).</summary>
public class ModifyProductsWordingTests
{
    // TEST-BB-1412: the Modify products introduction names each modification type (it listed eight of twelve after
    // freeze, deglycerolize, plasma reduction and rejuvenation were added); a new type fails here until it is named.
    [Fact]
    public void Introduction_NamesEveryModificationType()
    {
        var words = new Dictionary<ModificationType, string>
        {
            [ModificationType.Divide] = "divide", [ModificationType.Pool] = "pool", [ModificationType.Aliquot] = "aliquot",
            [ModificationType.Irradiate] = "irradiate", [ModificationType.Thaw] = "thaw", [ModificationType.Wash] = "wash",
            [ModificationType.VolumeReduction] = "volume-reduce", [ModificationType.Leukoreduction] = "leukoreduce",
            [ModificationType.Freeze] = "freeze", [ModificationType.Deglycerolize] = "deglycerolize",
            [ModificationType.PlasmaReduction] = "plasma-reduce", [ModificationType.Rejuvenate] = "rejuvenate"
        };
        Assert.All(Enum.GetValues<ModificationType>(), t => Assert.True(words.ContainsKey(t), $"Add wording for {t}."));

        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "BloodBankLIS.slnx")))
        {
            dir = dir.Parent;
        }

        var page = File.ReadAllText(Path.Combine(dir!.FullName, "src", "BloodBankLIS.Web", "Components", "Pages", "Modifications.razor")).ToLowerInvariant();
        var intro = page[page.IndexOf("<p class=\"muted\">", StringComparison.Ordinal)..page.IndexOf("</p>", StringComparison.Ordinal)];
        Assert.All(words.Values, w => Assert.Contains(w, intro));
    }
}
