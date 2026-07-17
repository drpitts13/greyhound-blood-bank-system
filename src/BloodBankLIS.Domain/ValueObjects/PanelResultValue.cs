using System.Text.Json;
using System.Text.Json.Serialization;

namespace BloodBankLIS.Domain.ValueObjects;

/// <summary>
/// JSON encoding for reaction-grade panel results (non-ABO/Rh) in <see cref="Entities.TestResult.Value"/>.
/// </summary>
public static class PanelResultValue
{
    private const int PanelVersion = 1;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static string Format(IReadOnlyDictionary<string, string> subtests)
    {
        var dto = new PanelDto(PanelVersion, subtests);
        return JsonSerializer.Serialize(dto, JsonOptions);
    }

    public static bool TryParse(string? value, out IReadOnlyDictionary<string, string> subtests)
    {
        subtests = new Dictionary<string, string>();
        if (string.IsNullOrWhiteSpace(value) || !value.TrimStart().StartsWith('{'))
        {
            return false;
        }

        try
        {
            var dto = JsonSerializer.Deserialize<PanelDto>(value, JsonOptions);
            if (dto is null || dto.Version != PanelVersion || dto.Subtests is null)
            {
                return false;
            }

            subtests = dto.Subtests;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public static string FormatDisplay(string? value)
    {
        if (!TryParse(value, out var subtests) || subtests.Count == 0)
        {
            return value ?? string.Empty;
        }

        return string.Join(" · ", subtests.Select(kv => $"{kv.Key}:{kv.Value}"));
    }

    private sealed record PanelDto(int Version, IReadOnlyDictionary<string, string>? Subtests);
}
