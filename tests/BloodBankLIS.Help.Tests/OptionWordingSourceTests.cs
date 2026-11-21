using System.Text.RegularExpressions;

namespace BloodBankLIS.Help.Tests;

/// <summary>Dropdown choices written into pages read as words (end-user testing loop 2 tick 8).</summary>
public partial class OptionWordingSourceTests
{
    // TEST-BB-1390: no page writes a choice as a run-together code name (the billing trigger lists offered UnitIssued,
    // UnitTransfused, TestVerified, ActionCompleted); a choice built from an enum goes through Ui.EnumText instead.
    [Fact]
    public void PageOptions_AreNotCodeNames()
    {
        var root = RepoRoot();
        var offenders = Directory.GetFiles(Path.Combine(root, "src", "BloodBankLIS.Web", "Components"), "*.razor", SearchOption.AllDirectories)
            .SelectMany(f => CodeNameOption().Matches(File.ReadAllText(f)).Select(m => $"{Path.GetFileName(f)}: {m.Groups[1].Value}"))
            .ToList();
        Assert.Empty(offenders);
    }

    // TEST-BB-1407 (end-user testing loop 2 tick 29): an enum field is not printed raw into markup (Crossmatch Settings
    // read "Complex Crossmatch (CXM) — ComplexCrossmatch", Transfusion Criteria "RedBloodCells"); it goes through Ui.EnumText.
    [Fact]
    public void PageMarkup_DoesNotPrintEnumFieldsRaw()
    {
        var root = RepoRoot();
        var offenders = Directory.GetFiles(Path.Combine(root, "src", "BloodBankLIS.Web", "Components"), "*.razor", SearchOption.AllDirectories)
            .SelectMany(f => RawEnumField().Matches(File.ReadAllText(f)).Select(m => $"{Path.GetFileName(f)}: {m.Value.Trim()}"))
            .ToList();
        Assert.Empty(offenders);
    }

    [GeneratedRegex(@"(?:^|[^\w(])@[A-Za-z_]+\.(?:ResultValueType|ModificationType|ComponentClass|EventType|IssueType)\s*(?:<|&| —| ·)", System.Text.RegularExpressions.RegexOptions.Multiline)]
    private static partial Regex RawEnumField();

    [GeneratedRegex(@">\s*([A-Z][a-z]+(?:[A-Z][a-z0-9]+)+)\s*</option>")]
    private static partial Regex CodeNameOption();

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "BloodBankLIS.slnx")))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new InvalidOperationException("Could not find the repository root.");
    }
}

/// <summary>Unit ABO/Rh checks go through the pool-aware check (mixed-ABO pooling phase 2).</summary>
public partial class AboCheckRoutingSourceTests
{
    // TEST-BB-1416: outside the rule itself and the standard product table builder (which compares single groups), no
    // code calls AboCompatibilityRule.Evaluate directly; a unit is checked with EvaluateUnit so a pool is checked against
    // each component and a pool without components is refused.
    [Fact]
    public void UnitChecks_UseEvaluateUnit()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "BloodBankLIS.slnx")))
        {
            dir = dir.Parent;
        }

        var allowed = new[] { "AboCompatibilityRule.cs", "ProductAboRhPolicy.cs" };
        var offenders = Directory.GetFiles(Path.Combine(dir!.FullName, "src"), "*.cs", SearchOption.AllDirectories)
            .Where(f => !allowed.Contains(Path.GetFileName(f)) && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .Where(f => File.ReadAllText(f).Contains("AboCompatibilityRule.Evaluate(", StringComparison.Ordinal))
            .Select(Path.GetFileName)
            .ToList();
        Assert.Empty(offenders);
    }
}

/// <summary>Admin › Exceptions says which definitions the software consults (end-user testing loop 2 tick 47).</summary>
public partial class ExceptionOverrideCodesSourceTests
{
    // TEST-BB-1418: every place that looks up an exception definition before an override names a code listed in
    // ExceptionOverridePolicy.OverrideCodes, so the Exceptions page's "Used for" column marks exactly those as
    // overridable and every other definition (ABO incompatibility, for one) as reference only.
    [Fact]
    public void EveryOverrideLookup_IsListedInOverrideCodes()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "BloodBankLIS.slnx")))
        {
            dir = dir.Parent;
        }

        var src = Path.Combine(dir!.FullName, "src");
        var policy = File.ReadAllText(Path.Combine(src, "BloodBankLIS.Domain", "Rules", "ExceptionOverridePolicy.cs"));
        var listed = policy[policy.IndexOf("OverrideCodes =", StringComparison.Ordinal)..policy.IndexOf("];", StringComparison.Ordinal)];
        var codes = Directory.GetFiles(src, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .SelectMany(f => OverrideLookup().Matches(File.ReadAllText(f)).Select(m => m.Groups[1].Value))
            .Distinct()
            .ToList();
        Assert.NotEmpty(codes);
        Assert.All(codes, code => Assert.Contains(code, listed));
        Assert.Equal(codes.Count, listed.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Count(p => p.Contains('.')));
    }

    [GeneratedRegex(@"ExceptionOverridePolicy\.EvaluateAccess\([^;]*?,\s*([A-Za-z]+\.[A-Za-z]+)\s*\)", System.Text.RegularExpressions.RegexOptions.Singleline)]
    private static partial Regex OverrideLookup();
}

/// <summary>The Modification Rules list names its products (end-user testing loop 2 tick 48).</summary>
public class ModificationRuleListSourceTests
{
    // TEST-BB-1419: the Modification Rules list shows the source and target product names under their codes, as the
    // form's pickers do, so a rule reads "E0336 Red Blood Cells, leukoreduced → E0332 …" rather than two bare codes.
    [Fact]
    public void RuleList_ShowsProductNamesUnderCodes()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "BloodBankLIS.slnx")))
        {
            dir = dir.Parent;
        }

        var page = File.ReadAllText(Path.Combine(dir!.FullName, "src", "BloodBankLIS.Web", "Components", "Pages", "Admin", "AdminModificationRules.razor"));
        Assert.Contains("@ProductName(r.SourceProductTypeId)", page);
        Assert.Contains("@ProductName(r.TargetProductTypeId)", page);
    }
}

/// <summary>A typed or scanned value with no match says so (end-user testing loop 2 tick 50).</summary>
public class ComboBoxNoMatchSourceTests
{
    // TEST-BB-1420: the shared combo box shows its no-match line whenever text is typed and nothing matches, even when
    // the list has a clear row (a quarantined unit scanned on Modify products showed only "—"), and the unit picker
    // says which units it offers for every scope.
    [Fact]
    public void NoMatch_IsShownForClearableLists_AndTheUnitPickerNamesItsScope()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "BloodBankLIS.slnx")))
        {
            dir = dir.Parent;
        }

        var shared = Path.Combine(dir!.FullName, "src", "BloodBankLIS.Web", "Components", "Shared");
        var combo = File.ReadAllText(Path.Combine(shared, "FilterableComboBox.razor"));
        Assert.Contains("(!_showClearOption || !string.IsNullOrWhiteSpace(_inputText))", combo);
        Assert.Contains("@NoMatchText", combo);

        var picker = File.ReadAllText(Path.Combine(shared, "UnitPickerComboBox.razor"));
        Assert.Contains("NoMatchText=\"@NoMatch\"", picker);
        foreach (var scope in Enum.GetNames(typeof(BloodBankLIS.Web.Components.Shared.UnitPickerScope)).Where(s => s != "OnHand"))
        {
            Assert.Contains($"UnitPickerScope.{scope} =>", picker);
        }
    }
}

/// <summary>Modify products names the product each rule makes (end-user testing loop 2 tick 51).</summary>
public class ModifyChoiceSourceTests
{
    // TEST-BB-1421: each modification choice on Modify products names the target product after its code
    // ("Irradiate → E0332 Red Blood Cells, Irradiated"), not the code alone.
    [Fact]
    public void ModificationChoice_NamesTheTargetProduct()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "BloodBankLIS.slnx")))
        {
            dir = dir.Parent;
        }

        var page = File.ReadAllText(Path.Combine(dir!.FullName, "src", "BloodBankLIS.Web", "Components", "Pages", "Modifications.razor"));
        Assert.Contains("@e.TargetProductCode@(TargetName(e.TargetProductTypeId))", page);
    }
}

