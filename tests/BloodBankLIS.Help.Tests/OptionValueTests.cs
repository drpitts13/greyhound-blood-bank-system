using System.Text.RegularExpressions;
using Bunit;

namespace BloodBankLIS.Help.Tests;

/// <summary>
/// The bedside "Transfusion reaction suspected?" select (Cycle 596) bound a bool? with options valued @false and @true:
/// those render as no value attribute and an empty one, and a select cannot bind a bool at all (the binder casts the
/// checkbox's true or false), so no answer ever took and every finish was refused until Cycle 624. A yes/no select binds
/// through text; InspectionAnswer is the button form.
/// </summary>
public partial class OptionValueTests
{
    // TEST-BB-749: no page writes a boolean option value as a C# literal, and no select binds a bool field directly.
    [Fact]
    public void Options_DoNotUseBooleanLiteralValues()
    {
        var root = RepoRoot();
        var boolFields = Directory.EnumerateFiles(Path.Combine(root, "src", "BloodBankLIS.Web", "Components"), "*.razor", SearchOption.AllDirectories)
            .SelectMany(f => BoolField().Matches(File.ReadAllText(f)).Select(m => (File: f, Name: m.Groups[1].Value)))
            .ToList();
        var boundSelects = boolFields
            .Where(b => Regex.IsMatch(File.ReadAllText(b.File), $@"<select[^>]*@bind(-value)?=""{Regex.Escape(b.Name)}"""))
            .Select(b => $"{Path.GetRelativePath(root, b.File)}: select bound to bool {b.Name}")
            .ToList();
        Assert.True(boundSelects.Count == 0, "A select is bound to a bool (bind it through text):" + Environment.NewLine + string.Join(Environment.NewLine, boundSelects));

        var problems = Directory.EnumerateFiles(Path.Combine(root, "src", "BloodBankLIS.Web", "Components"), "*.razor", SearchOption.AllDirectories)
            .SelectMany(f => File.ReadAllLines(f).Select((line, i) => (File: f, Line: i + 1, Text: line)))
            .Where(l => BooleanOptionValue().IsMatch(l.Text))
            .Select(l => $"{Path.GetRelativePath(root, l.File)}:{l.Line}: {l.Text.Trim()}")
            .ToList();

        Assert.True(problems.Count == 0,
            "An option value is a C# boolean literal (write value=\"true\" or value=\"false\"):" + Environment.NewLine + string.Join(Environment.NewLine, problems));
    }

    [GeneratedRegex(@"\bbool\??\s+(_?[A-Za-z]\w*)\s*(;|=|\{)")]
    private static partial Regex BoolField();

    [GeneratedRegex(@"value=""@\(?(true|false)\)?""")]
    private static partial Regex BooleanOptionValue();

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "BloodBankLIS.slnx")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Could not find the repository root.");
    }
}

/// <summary>
/// The types bound to select boxes, through Blazor's own converter (Cycle 625): "" is unanswered and an option's
/// rendered value converts to the answer. A bool is the one that does not (the binder casts a checkbox's true or false),
/// which is why the bedside reaction question failed and binds through text.
/// </summary>
public class SelectBindingTests
{
    private static (bool Ok, T? Value) Convert<T>(string text) =>
        Microsoft.AspNetCore.Components.BindConverter.TryConvertTo<T>(text, System.Globalization.CultureInfo.CurrentCulture, out var value)
            ? (true, value)
            : (false, default);

    // TEST-BB-750: each nullable enum and id bound to a select takes "" as unanswered and its option value as the answer.
    [Fact]
    public void NullableSelectTypes_Bind()
    {
        Assert.Equal((true, (BloodBankLIS.Domain.Enums.AboGroup?)null), Convert<BloodBankLIS.Domain.Enums.AboGroup?>(""));
        foreach (var a in Enum.GetValues<BloodBankLIS.Domain.Enums.AboGroup>())
            Assert.Equal((true, (BloodBankLIS.Domain.Enums.AboGroup?)a), Convert<BloodBankLIS.Domain.Enums.AboGroup?>(a.ToString()));
        foreach (var r in Enum.GetValues<BloodBankLIS.Domain.Enums.RhType>())
            Assert.Equal((true, (BloodBankLIS.Domain.Enums.RhType?)r), Convert<BloodBankLIS.Domain.Enums.RhType?>(r.ToString()));
        foreach (var x in Enum.GetValues<BloodBankLIS.Domain.Enums.CrossmatchResult>())
            Assert.Equal((true, (BloodBankLIS.Domain.Enums.CrossmatchResult?)x), Convert<BloodBankLIS.Domain.Enums.CrossmatchResult?>(x.ToString()));
        foreach (var d in Enum.GetValues<BloodBankLIS.Domain.Enums.TransfusionDisposition>())
            Assert.Equal((true, (BloodBankLIS.Domain.Enums.TransfusionDisposition?)d), Convert<BloodBankLIS.Domain.Enums.TransfusionDisposition?>(d.ToString()));
        foreach (var s in Enum.GetValues<BloodBankLIS.Domain.Enums.Sex>())
            Assert.Equal((true, (BloodBankLIS.Domain.Enums.Sex?)s), Convert<BloodBankLIS.Domain.Enums.Sex?>(s.ToString()));
        foreach (var u in Enum.GetValues<BloodBankLIS.Domain.Enums.UnitStatus>())
            Assert.Equal((true, (BloodBankLIS.Domain.Enums.UnitStatus?)u), Convert<BloodBankLIS.Domain.Enums.UnitStatus?>(u.ToString()));
        Assert.Equal((true, (long?)null), Convert<long?>(""));
        Assert.Equal((true, (long?)42), Convert<long?>("42"));
    }

    // TEST-BB-751: the case that failed. A select's text cannot bind to a bool (the converter throws on the cast), and
    // the bedside question's text form does.
    [Fact]
    public void BoolSelect_DoesNotBind_TextDoes()
    {
        Assert.Throws<InvalidCastException>(() => Convert<bool?>("false"));
        // A plain bool fails the same way (Cycle 633: three more selects bound one).
        Assert.Throws<InvalidCastException>(() => Convert<bool>("true"));
        Assert.Equal((true, "no"), Convert<string>("no"));
    }
}

/// <summary>The yes/no selects fixed in Cycle 633 take the choice made.</summary>
public class YesNoSelectTests : Bunit.BunitContext
{
    private static T Field<T>(object page, string name) =>
        (T)page.GetType().GetField(name, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(page)!;

    // TEST-BB-767: Storage temperatures' "Excursions only" is taken when chosen and dropped when changed back (it stayed
    // All readings, as a select cannot bind a bool).
    [Fact]
    public void StorageTemperatures_ExcursionsOnly_IsTaken()
    {
        CannedApi.Register(this, new Dictionary<string, object>());
        Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions.AddSingleton(Services, new BloodBankLIS.Web.Services.ReportDownloadCache(new Microsoft.Extensions.Caching.Memory.MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions())));
        var page = Render<BloodBankLIS.Web.Components.Pages.StorageTempReport>();

        page.Find("[data-excursions-only]").Change("true");
        Assert.True(Field<bool>(page.Instance, "_excursionsOnly"));
        page.Find("[data-excursions-only]").Change("false");
        Assert.False(Field<bool>(page.Instance, "_excursionsOnly"));
    }
}
