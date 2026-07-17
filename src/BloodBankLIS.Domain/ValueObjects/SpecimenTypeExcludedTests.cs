using System.Text.Json;
using System.Text.Json.Serialization;

namespace BloodBankLIS.Domain.ValueObjects;

public static class SpecimenTypeExcludedTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static IReadOnlyList<string> Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Array.Empty<string>();
        }

        try
        {
            var raw = JsonSerializer.Deserialize<List<RowJson>>(json, JsonOptions);
            if (raw is null || raw.Count == 0)
            {
                return Array.Empty<string>();
            }

            return raw
                .Select(r => r.Code)
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Select(c => c!.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (JsonException)
        {
            return Array.Empty<string>();
        }
    }

    public static string Serialize(IEnumerable<string> testCodes)
    {
        var rows = testCodes
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(c => new RowJson { Code = c })
            .ToList();
        return JsonSerializer.Serialize(rows, JsonOptions);
    }

    private sealed class RowJson
    {
        [JsonPropertyName("code")]
        public string? Code { get; set; }
    }
}
