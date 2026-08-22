using System.Text.Json;
using System.Text.Json.Serialization;

namespace BloodBankLIS.Domain.ValueObjects;

/// <summary>
/// JSON encoding for reaction-grade panel results (non-ABO/Rh) in <see cref="Entities.TestResult.Value"/>.
/// Version 1 is a flat map of subtest code → grade. Version 2 nests phases under each subtest.
/// Parsed results are always flattened to composite keys (<c>Cell1|IS</c>) when phased.
/// </summary>
public static class PanelResultValue
{
    public const int FlatVersion = 1;
    public const int PhasedVersion = 2;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static string Format(IReadOnlyDictionary<string, string> subtests)
    {
        if (subtests.Keys.Any(k => PhaseResultKeys.TrySplit(k, out _, out _)))
        {
            return FormatPhased(subtests);
        }

        var dto = new FlatPanelDto(FlatVersion, subtests);
        return JsonSerializer.Serialize(dto, JsonOptions);
    }

    public static string FormatPhased(IReadOnlyDictionary<string, string> flattened)
    {
        var nested = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        var leftover = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (key, value) in flattened)
        {
            if (PhaseResultKeys.TrySplit(key, out var subtest, out var phase))
            {
                if (!nested.TryGetValue(subtest, out var phases))
                {
                    phases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    nested[subtest] = phases;
                }

                phases[phase] = value;
            }
            else
            {
                leftover[key] = value;
            }
        }

        if (leftover.Count > 0 && nested.Count == 0)
        {
            return JsonSerializer.Serialize(new FlatPanelDto(FlatVersion, leftover), JsonOptions);
        }

        foreach (var (code, grade) in leftover)
        {
            if (!nested.ContainsKey(code))
            {
                nested[code] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    [code] = grade
                };
            }
        }

        var dto = new PhasedPanelDto(PhasedVersion, nested);
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
            using var doc = JsonDocument.Parse(value);
            if (!doc.RootElement.TryGetProperty("version", out var versionProp)
                || !doc.RootElement.TryGetProperty("subtests", out var subtestsProp)
                || subtestsProp.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            var version = versionProp.GetInt32();
            if (version == PhasedVersion)
            {
                subtests = FlattenPhased(subtestsProp);
                return true;
            }

            if (version != FlatVersion)
            {
                return false;
            }

            var flat = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var prop in subtestsProp.EnumerateObject())
            {
                if (prop.Value.ValueKind == JsonValueKind.String)
                {
                    flat[prop.Name] = prop.Value.GetString() ?? string.Empty;
                }
            }

            subtests = flat;
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

    private static IReadOnlyDictionary<string, string> FlattenPhased(JsonElement subtestsProp)
    {
        var flat = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var cell in subtestsProp.EnumerateObject())
        {
            if (cell.Value.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            foreach (var phase in cell.Value.EnumerateObject())
            {
                if (phase.Value.ValueKind == JsonValueKind.String)
                {
                    flat[PhaseResultKeys.Compose(cell.Name, phase.Name)] = phase.Value.GetString() ?? string.Empty;
                }
            }
        }

        return flat;
    }

    private sealed record FlatPanelDto(int Version, IReadOnlyDictionary<string, string>? Subtests);

    private sealed record PhasedPanelDto(int Version, IReadOnlyDictionary<string, Dictionary<string, string>>? Subtests);
}
