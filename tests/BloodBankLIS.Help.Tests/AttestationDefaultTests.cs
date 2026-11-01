using System.Text.RegularExpressions;

namespace BloodBankLIS.Help.Tests;

/// <summary>
/// TEST-BB-314: attestations are made by a person. The override authorizer is never filled with the signed-in
/// user except by pressing "Me" (OCD-041), and return inspections never start as passed (OCD-040).
/// </summary>
public partial class AttestationDefaultTests
{
    private static readonly string Components = Path.Combine(RepoRoot(), "src", "BloodBankLIS.Web", "Components");

    [Fact]
    public void Override_authorizer_is_not_filled_with_the_signed_in_user()
    {
        var problems = Scan(AuthorizerFromSession(), skip: "AuthorizerField.razor");

        Assert.True(problems.Count == 0,
            "These lines put the signed-in user into an override authorizer; use AuthorizerField's Me button:" + Environment.NewLine
            + string.Join(Environment.NewLine, problems));
    }

    [Fact]
    public void Authorized_by_inputs_use_the_shared_field()
    {
        var problems = Scan(PlainAuthorizedByInput(), skip: "AuthorizerField.razor");

        Assert.True(problems.Count == 0, "Use <AuthorizerField> for Authorized by:" + Environment.NewLine + string.Join(Environment.NewLine, problems));
    }

    [Fact]
    public void Return_inspections_do_not_start_as_passed()
    {
        var problems = Scan(InspectionStartsTrue(), skip: null);

        Assert.True(problems.Count == 0,
            "Return inspections must start unanswered unless the facility default applies:" + Environment.NewLine
            + string.Join(Environment.NewLine, problems));
    }

    [Fact]
    public void Appearance_does_not_start_as_acceptable()
    {
        var problems = Scan(AppearanceStartsAcceptable(), skip: "AppearanceSelect.razor");

        Assert.True(problems.Count == 0,
            "Appearance must start unrecorded (AppearanceSelect applies the facility default):" + Environment.NewLine
            + string.Join(Environment.NewLine, problems));
    }

    [Fact]
    public void Appearance_inputs_use_the_shared_select()
    {
        var problems = Scan(PlainAppearanceSelect(), skip: "AppearanceSelect.razor");

        Assert.True(problems.Count == 0, "Use <AppearanceSelect> for appearance:" + Environment.NewLine + string.Join(Environment.NewLine, problems));
    }

    private static List<string> Scan(Regex pattern, string? skip)
    {
        var problems = new List<string>();
        foreach (var file in Directory.EnumerateFiles(Components, "*.razor", SearchOption.AllDirectories))
        {
            if (skip is not null && file.EndsWith(skip, StringComparison.Ordinal))
            {
                continue;
            }

            var text = File.ReadAllText(file);
            foreach (Match m in pattern.Matches(text))
            {
                var line = text[..m.Index].Count(c => c == '\n') + 1;
                problems.Add($"{Path.GetFileName(file)}:{line} {m.Value.Trim()}");
            }
        }

        return problems;
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

    // Assignments, fallbacks, or initializers that put Session.UserName into an *AuthorizedBy field.
    [GeneratedRegex(@"[Aa]uthorizedBy\w*\s*(?:=|\?\?)[^;\n]*Session\.UserName|IsNullOrWhiteSpace\(\s*_?\w*[Aa]uthorizedBy\w*\s*\)\s*\?\s*Session\.UserName")]
    private static partial Regex AuthorizerFromSession();

    [GeneratedRegex(@"<label>Authorized by</label>\s*<input")]
    private static partial Regex PlainAuthorizedByInput();

    [GeneratedRegex(@"_(?:return|change)(?:Temp|Seal|Visual)Ok\s*=\s*true\b")]
    private static partial Regex InspectionStartsTrue();

    // A field, property, or reset that sets an appearance to Acceptable without a person choosing it.
    [GeneratedRegex(@"(?:_\w*[Aa]ppearance|\bAppearance)\s*(?:\{\s*get;\s*set;\s*\}\s*)?=\s*UnitAppearance\.Acceptable\s*;")]
    private static partial Regex AppearanceStartsAcceptable();

    [GeneratedRegex(@"<select\b[^>]*@bind[^>]*[Aa]ppearance")]
    private static partial Regex PlainAppearanceSelect();
}
