using System.Text.Json;
using System.Text.Json.Serialization;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.ValueObjects;

/// <summary>
/// Canonical encoding for ABO/Rh results in <c>TestResult.Value</c>.
/// Legacy format: "<![CDATA[Abo|Rh]]>" (e.g. "O|Positive").
/// Panel format: JSON with version, interpreted type, and subtest reaction grades.
/// </summary>
public static class AboRhResultValue
{
    private const char Separator = '|';
    private const int PanelVersion = 1;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    public static string Format(AboGroup abo, RhType rh) => $"{abo}{Separator}{rh}";

    public static string Format(AboRh value) => Format(value.Abo, value.Rh);

    public static string FormatPanel(AboRhPanelResult panel)
    {
        var dto = new PanelDto(panel.Abo, panel.Rh, PanelVersion, panel.Subtests);
        return JsonSerializer.Serialize(dto, JsonOptions);
    }

    public static bool TryParse(string? value, out AboRh result)
    {
        result = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (TryParsePanel(value, out var panel))
        {
            result = panel.InterpretedType;
            return true;
        }

        var parts = value.Split(Separator);
        if (parts.Length != 2)
        {
            return false;
        }

        if (Enum.TryParse<AboGroup>(parts[0], out var abo) && Enum.TryParse<RhType>(parts[1], out var rh))
        {
            result = new AboRh(abo, rh);
            return true;
        }

        return false;
    }

    public static bool TryParsePanel(string? value, out AboRhPanelResult panel)
    {
        panel = default!;
        if (string.IsNullOrWhiteSpace(value) || !value.TrimStart().StartsWith('{'))
        {
            return false;
        }

        try
        {
            var dto = JsonSerializer.Deserialize<PanelDto>(value, JsonOptions);
            if (dto is null || dto.Version != PanelVersion)
            {
                return false;
            }

            panel = new AboRhPanelResult(dto.Abo, dto.Rh, dto.Subtests ?? new Dictionary<string, string>());
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public static string FormatDisplay(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        if (TryParsePanel(value, out var panel))
        {
            var parts = new List<string> { panel.InterpretedType.ToString() };
            foreach (var code in AboRhPanelSubtestCodes.All)
            {
                if (panel.Subtests.TryGetValue(code, out var grade) && !string.IsNullOrWhiteSpace(grade)
                    && !string.Equals(grade, "NT", StringComparison.OrdinalIgnoreCase))
                {
                    parts.Add($"{code}:{grade}");
                }
            }

            return string.Join(" · ", parts);
        }

        return TryParse(value, out var simple) ? simple.ToString() : value;
    }

    private sealed record PanelDto(
        AboGroup Abo,
        RhType Rh,
        int Version,
        IReadOnlyDictionary<string, string>? Subtests);
}
