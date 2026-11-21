namespace BloodBankLIS.Help.Tests;

public class InstrumentAdminListSourceTests
{
    // TEST-BB-1263: the instrument list names its counts in their own columns, Field maps and Translations, instead of
    // "16 / 43" under Maps (end-user testing pass 239).
    [Fact]
    public void InstrumentList_NamesFieldMapsAndTranslations()
    {
        var page = File.ReadAllText(Path.Combine(RepoRoot(), "src", "BloodBankLIS.Web", "Components", "Pages", "Admin", "AdminInstruments.razor"));
        Assert.Contains("<th>Field maps</th><th>Translations</th>", page);
        Assert.Contains("<td data-instrument-field-maps>@item.FieldMappings.Count</td><td data-instrument-translations>@item.Translations.Count</td>", page);
        Assert.DoesNotContain("<th>Maps</th>", page);
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "BloodBankLIS.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Could not find the repository root.");
    }
}
