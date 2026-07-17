using System.Text.Json;
using System.Text.Json.Serialization;

namespace BloodBankLIS.Domain.ValueObjects;

public sealed record TestGrouperMember(string TestCode, int SortOrder = 0);

public static class TestGrouperMembers
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static string? ToJson(IReadOnlyList<TestGrouperMember>? items)
    {
        if (items is null || items.Count == 0)
        {
            return null;
        }

        return JsonSerializer.Serialize(items.OrderBy(i => i.SortOrder).ThenBy(i => i.TestCode), JsonOptions);
    }

    public static IReadOnlyList<TestGrouperMember> Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Array.Empty<TestGrouperMember>();
        }

        try
        {
            var items = JsonSerializer.Deserialize<List<TestGrouperMember>>(json, JsonOptions);
            return items is null || items.Count == 0
                ? Array.Empty<TestGrouperMember>()
                : items.OrderBy(i => i.SortOrder).ThenBy(i => i.TestCode).ToList();
        }
        catch (JsonException)
        {
            return Array.Empty<TestGrouperMember>();
        }
    }
}
