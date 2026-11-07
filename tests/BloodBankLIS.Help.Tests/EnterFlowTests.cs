using System.Text.RegularExpressions;

namespace BloodBankLIS.Help.Tests;

/// <summary>
/// TEST-BB-313: Enter moves between identifier and unit-label fields at issue, ward receipt, and bedside
/// (wwwroot/ui.js). A scanner ends every scan with Enter, so Enter must never land on, or press, the
/// button that issues, receives, or documents.
/// </summary>
public partial class EnterFlowTests
{
    private static readonly string Root = RepoRoot();
    private static readonly string Components = Path.Combine(Root, "src", "BloodBankLIS.Web", "Components");

    [Fact]
    public void Only_text_inputs_are_enter_steps()
    {
        var problems = new List<string>();
        foreach (var file in Directory.EnumerateFiles(Components, "*.razor", SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(file);
            foreach (Match tag in TagWithStep().Matches(text))
            {
                if (!tag.Value.StartsWith("<input", StringComparison.Ordinal) || NonTextType().IsMatch(tag.Value))
                {
                    problems.Add($"{Path.GetRelativePath(Root, file)}: {tag.Value}");
                }
            }
        }

        Assert.True(problems.Count == 0, "data-enter-step belongs on text inputs only:" + Environment.NewLine + string.Join(Environment.NewLine, problems));
    }

    [Theory]
    [InlineData("Pages/Issuing.razor", 3)]
    [InlineData("Shared/PatientChartProductsTab.razor", 1)]
    public void Issue_and_bedside_forms_are_enter_flows_with_identifier_steps(string relative, int expectedFlows)
    {
        var text = File.ReadAllText(Path.Combine(Components, relative));

        Assert.Equal(expectedFlows, Regex.Matches(text, @"\bdata-enter-flow\b").Count);
        Assert.Contains("placeholder=\"Scan wristband or type\" data-enter-step", text);
        Assert.DoesNotContain("FocusOnEnterAsync", text);
        Assert.DoesNotContain("OnId1KeyDownAsync", text);
    }

    [Fact]
    public void Unit_label_scan_fields_are_steps()
    {
        var text = File.ReadAllText(Path.Combine(Components, "Shared", "UnitScanVerificationPanel.razor"));

        Assert.Equal(Regex.Matches(text, "<input").Count, Regex.Matches(text, "data-enter-step").Count);
    }

    [Fact]
    public void Script_never_focuses_or_clicks_a_button_after_the_last_step()
    {
        var script = File.ReadAllText(Path.Combine(Root, "src", "BloodBankLIS.Web", "wwwroot", "ui.js"));
        var start = script.IndexOf("Enter moves to the next step", StringComparison.Ordinal);
        var end = script.IndexOf("shows the keyboard shortcuts", start, StringComparison.Ordinal);
        var flow = script[start..end];

        Assert.Contains("input[data-enter-step]", flow);
        Assert.Contains("flow.focus(", flow);
        Assert.DoesNotContain(".click(", flow);
        Assert.DoesNotContain("button", flow.Replace("NEVER to a button", string.Empty, StringComparison.Ordinal), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("requestSubmit", flow);
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

    [GeneratedRegex(@"<\w+\b[^>]*\bdata-enter-step\b[^>]*>", RegexOptions.Singleline)]
    private static partial Regex TagWithStep();

    [GeneratedRegex("type=\"(?:checkbox|radio|button|submit|hidden|file)\"")]
    private static partial Regex NonTextType();
}
