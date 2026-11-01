using System.Text.RegularExpressions;

namespace BloodBankLIS.Help.Tests;

/// <summary>
/// A button disabled until a text field has a value must see the value as it is typed. With the
/// default change binding the field updates only on blur, so the button stays disabled while typing
/// and the first click only moves focus out of the field.
/// </summary>
public partial class RazorFormBindingTests
{
    [Fact]
    public void Text_fields_that_enable_a_button_bind_on_input()
    {
        var root = RepoRoot();
        var files = Directory.EnumerateFiles(Path.Combine(root, "src", "BloodBankLIS.Web", "Components"), "*.razor", SearchOption.AllDirectories);
        var problems = new List<string>();

        foreach (var file in files)
        {
            var text = File.ReadAllText(file);
            var properties = BoolProperty().Matches(text).ToDictionary(m => m.Groups[1].Value, m => m.Groups[2].Value);
            var gated = new HashSet<string>(StringComparer.Ordinal);

            foreach (Match disabled in DisabledExpression().Matches(text))
            {
                var pending = new Stack<string>([disabled.Groups[1].Value]);
                var seen = new HashSet<string>(StringComparer.Ordinal);
                while (pending.Count > 0)
                {
                    var expression = pending.Pop();
                    foreach (Match field in NullOrEmptyCheck().Matches(expression))
                        gated.Add(field.Groups.Values.Skip(1).First(g => g.Success).Value);
                    foreach (Match name in PropertyName().Matches(expression))
                    {
                        if (properties.TryGetValue(name.Value, out var body) && seen.Add(name.Value))
                            pending.Push(body);
                    }
                }
            }

            foreach (var field in gated)
            {
                var input = new Regex("<(?:input|textarea)\\b[^>]*@bind(?:-value)?=\"" + Regex.Escape(field) + "\"[^>]*>");
                foreach (Match tag in input.Matches(text))
                {
                    if (tag.Value.Contains("oninput", StringComparison.Ordinal) || NonTextType().IsMatch(tag.Value))
                        continue;
                    var line = text[..tag.Index].Count(c => c == '\n') + 1;
                    problems.Add($"{Path.GetRelativePath(root, file)}:{line} {field}");
                }
            }
        }

        Assert.True(problems.Count == 0,
            "These fields enable a button but update only on blur; add @bind:event=\"oninput\":" + Environment.NewLine
            + string.Join(Environment.NewLine, problems));
    }

    /// <summary>
    /// A New/Edit editor rendered below a list opens off-screen on a long page, so the click looks
    /// like it did nothing. Top-level editor cards carry data-scroll-on-show (wwwroot/ui.js).
    /// </summary>
    [Fact]
    public void Editor_cards_scroll_into_view_when_opened()
    {
        var root = RepoRoot();
        var files = Directory.EnumerateFiles(Path.Combine(root, "src", "BloodBankLIS.Web", "Components", "Pages"), "*.razor", SearchOption.AllDirectories);
        var problems = new List<string>();

        foreach (var file in files)
        {
            var lines = File.ReadAllLines(file);
            for (var i = 0; i < lines.Length; i++)
            {
                if (!EditorGuard().IsMatch(lines[i]))
                    continue;
                var card = lines.Skip(i + 1).Take(3).FirstOrDefault(l => l.Contains("class=\"bb-card", StringComparison.Ordinal));
                if (card is not null && !card.Contains("data-scroll-on-show", StringComparison.Ordinal))
                    problems.Add($"{Path.GetRelativePath(root, file)}:{i + 1}");
            }
        }

        Assert.True(problems.Count == 0,
            "These editor cards open below the list without data-scroll-on-show:" + Environment.NewLine
            + string.Join(Environment.NewLine, problems));
    }

    /// <summary>
    /// Razor passes a string component parameter as literal text unless it starts with @, so
    /// Current="_tab" hands the child the word "_tab" and no chart tab is ever highlighted.
    /// A string parameter given a bare field name (_x) or member path (a.b) must use @.
    /// </summary>
    [Fact]
    public void String_parameters_are_not_given_a_field_name_as_literal_text()
    {
        var root = RepoRoot();
        var files = Directory.EnumerateFiles(Path.Combine(root, "src", "BloodBankLIS.Web", "Components"), "*.razor", SearchOption.AllDirectories).ToList();
        var stringParameters = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var file in files)
        {
            var names = StringParameter().Matches(File.ReadAllText(file)).Select(m => m.Groups[1].Value);
            stringParameters[Path.GetFileNameWithoutExtension(file)] = new HashSet<string>(names, StringComparer.Ordinal);
        }

        var problems = new List<string>();
        foreach (var file in files)
        {
            var text = File.ReadAllText(file);
            foreach (Match tag in ComponentTag().Matches(text))
            {
                if (!stringParameters.TryGetValue(tag.Groups[1].Value, out var parameters))
                    continue;
                foreach (Match attribute in BareIdentifierAttribute().Matches(tag.Groups[2].Value))
                {
                    var value = attribute.Groups[2].Value;
                    if (parameters.Contains(attribute.Groups[1].Value) && (value.StartsWith('_') || value.Contains('.')))
                    {
                        var line = text[..tag.Index].Count(c => c == '\n') + 1;
                        problems.Add($"{Path.GetRelativePath(root, file)}:{line} {attribute.Value}");
                    }
                }
            }
        }

        Assert.True(problems.Count == 0,
            "These string parameters receive a field name as literal text; prefix the value with @:" + Environment.NewLine
            + string.Join(Environment.NewLine, problems));
    }

    /// <summary>
    /// OCD-039: the two patient identifiers at issue and bedside are what staff read from the wristband or
    /// requisition. A page must not copy the record's MRN or date of birth into them except through
    /// PatientIdentifierEntryRule.Prefill, which honours the facility policy (default off).
    /// </summary>
    [Fact]
    public void Patient_identifier_fields_are_not_filled_from_the_record_directly()
    {
        var root = RepoRoot();
        var files = Directory.EnumerateFiles(Path.Combine(root, "src", "BloodBankLIS.Web", "Components"), "*.razor", SearchOption.AllDirectories);
        var problems = new List<string>();
        foreach (var file in files)
        {
            var text = File.ReadAllText(file);
            foreach (Match m in IdentifierFromRecord().Matches(text))
            {
                var line = text[..m.Index].Count(c => c == '\n') + 1;
                problems.Add($"{Path.GetRelativePath(root, file)}:{line} {m.Value.Trim()}");
            }
        }

        Assert.True(problems.Count == 0,
            "These lines copy the record into a patient identifier field; use PatientIdentifierEntryRule.Prefill:" + Environment.NewLine
            + string.Join(Environment.NewLine, problems));
    }

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

    [GeneratedRegex("disabled=\"@\\(?([^\"]*?)\\)?\"")]
    private static partial Regex DisabledExpression();

    [GeneratedRegex(@"IsNullOrWhiteSpace\(([\w.]+)\)|IsNullOrEmpty\(([\w.]+)\)|\b(_\w+) is null")]
    private static partial Regex NullOrEmptyCheck();

    [GeneratedRegex(@"bool\s+(\w+)\s*=>\s*([^;]+);", RegexOptions.Singleline)]
    private static partial Regex BoolProperty();

    [GeneratedRegex(@"\b[A-Z]\w+\b")]
    private static partial Regex PropertyName();

    [GeneratedRegex("type=\"(?:checkbox|date|datetime-local|time|file|hidden)\"")]
    private static partial Regex NonTextType();

    [GeneratedRegex(@"\[Parameter[^\]]*\]\s*public\s+string\??\s+(\w+)")]
    private static partial Regex StringParameter();

    [GeneratedRegex(@"<([A-Z]\w+)\b([^>]*)>", RegexOptions.Singleline)]
    private static partial Regex ComponentTag();

    // Name="value" preceded by whitespace, so @bind-Name="..." is not matched.
    [GeneratedRegex(@"(?<=\s)(\w+)=""([A-Za-z_]\w*(?:\.\w+)*)""")]
    private static partial Regex BareIdentifierAttribute();

    [GeneratedRegex(@"(?:PatientIdentifier[12]Value|_issueId[12]|_txPatientIdentifier[12])\s*=\s*[^;=]*(?:MedicalRecordNumber|DateOfBirth)[^;]*;")]
    private static partial Regex IdentifierFromRecord();

    // _editing, or any _draft / _xxxDraft editor, optionally with extra && conditions (e.g. CanEdit).
    [GeneratedRegex(@"^@if \((?:_editing|_editing is not null|_\w*[dD]raft is not null)(?: && .*)?\)\s*$")]
    private static partial Regex EditorGuard();
}
