using System.Text.RegularExpressions;
using BloodBankLIS.Domain.Rules.Engine;

namespace BloodBankLIS.Domain.Instruments;

public sealed record InstrumentRuleSetAction(string Field, string Value);

/// <summary>Parses <c>set('field','value')</c> actions used by instrument interface rules.</summary>
public static partial class InstrumentRuleActions
{
    [GeneratedRegex(@"^\s*set\s*\(\s*'([^']+)'\s*,\s*'([^']*)'\s*\)\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex SetCall();

    public static bool TryParse(string? text, out IReadOnlyList<InstrumentRuleSetAction> actions, out string? error)
    {
        actions = [];
        error = null;
        if (string.IsNullOrWhiteSpace(text))
        {
            error = "Action is empty.";
            return false;
        }

        var parsed = new List<InstrumentRuleSetAction>();
        foreach (var part in text.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var match = SetCall().Match(part);
            if (!match.Success)
            {
                error = $"Unsupported action '{part}'. Use set('field','value').";
                return false;
            }

            var field = match.Groups[1].Value.Trim();
            if (!InstrumentDataItemKeys.All.Contains(field, StringComparer.OrdinalIgnoreCase)
                && !field.StartsWith("message.", StringComparison.OrdinalIgnoreCase))
            {
                error = $"Unknown action field '{field}'.";
                return false;
            }

            parsed.Add(new InstrumentRuleSetAction(NormalizeField(field), match.Groups[2].Value));
        }

        if (parsed.Count == 0)
        {
            error = "Action is empty.";
            return false;
        }

        actions = parsed;
        return true;
    }

    public static string NormalizeField(string field) =>
        field.StartsWith("message.", StringComparison.OrdinalIgnoreCase)
            ? field["message.".Length..]
            : field;

    public static RuleFactBag ToFacts(IReadOnlyDictionary<string, string?> values)
    {
        var bag = new RuleFactBag();
        foreach (var key in InstrumentDataItemKeys.All)
        {
            values.TryGetValue(key, out var value);
            bag.Set(key, value);
            bag.Set($"message.{key}", value);
        }

        return bag;
    }
}
