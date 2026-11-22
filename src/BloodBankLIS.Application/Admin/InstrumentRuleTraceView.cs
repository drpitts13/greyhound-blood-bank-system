using System.Text.Json;

namespace BloodBankLIS.Application.Admin;

/// <summary>One rule that ran on an instrument message: its name, condition, what it changed, and how many times in a row.</summary>
public sealed record InstrumentRuleTraceRow(string Rule, string? Condition, string Changes, int Times);

/// <summary>
/// End-user testing pass 254: an instrument message's rule trace was shown as raw JSON (five identical "Numeric 0" blocks
/// for one ABO/Rh panel). It is now read as rows: rule, condition, what it changed, with the same rule and change in a row
/// counted once ("× 5"). A trace that is not the expected JSON gives no rows, and the raw text is still shown.
/// </summary>
public static class InstrumentRuleTraceView
{
    public static IReadOnlyList<InstrumentRuleTraceRow> Rows(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            var rows = new List<InstrumentRuleTraceRow>();
            foreach (var item in doc.RootElement.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var rule = Text(item, "ruleName") ?? "Rule";
                var condition = Text(item, "condition");
                var changes = item.TryGetProperty("changes", out var c) && c.ValueKind == JsonValueKind.Object
                    ? string.Join(", ", c.EnumerateObject().Select(p => $"{p.Name} → {(p.Value.ValueKind == JsonValueKind.String ? p.Value.GetString() : p.Value.ToString())}"))
                    : Text(item, "action") ?? string.Empty;
                if (string.IsNullOrEmpty(changes))
                {
                    changes = "No change";
                }

                var last = rows.Count == 0 ? null : rows[^1];
                if (last is not null && last.Rule == rule && last.Condition == condition && last.Changes == changes)
                {
                    rows[^1] = last with { Times = last.Times + 1 };
                }
                else
                {
                    rows.Add(new InstrumentRuleTraceRow(rule, condition, changes, 1));
                }
            }

            return rows;
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static string? Text(JsonElement item, string name) =>
        item.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(v.GetString())
            ? v.GetString()!.Trim()
            : null;
}
