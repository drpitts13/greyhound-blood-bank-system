using System.Globalization;
using System.Text.Json;

namespace BloodBankLIS.Domain.ValueObjects;

/// <summary>
/// Vital signs taken at the bedside before a transfusion starts and after it ends (end-user testing, 2026-11-12: the
/// transfusion record had fields for them that nothing filled, so a reaction workup had no baseline temperature). Each
/// sign is optional and none is required: whether and when vitals are taken is the facility's policy. The ranges only
/// catch a mistyped value (a temperature of 370, a pulse of 8); they are not clinical limits.
/// </summary>
public sealed record TransfusionVitals(
    decimal? TemperatureC = null,
    int? Pulse = null,
    int? SystolicBp = null,
    int? DiastolicBp = null,
    int? RespiratoryRate = null)
{
    public bool IsEmpty => TemperatureC is null && Pulse is null && SystolicBp is null && DiastolicBp is null && RespiratoryRate is null;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Each problem with what was typed, or none. A blood pressure needs both numbers.</summary>
    public IReadOnlyList<string> Problems(string when)
    {
        var problems = new List<string>();
        if (TemperatureC is { } t && (t < 25m || t > 45m))
        {
            problems.Add($"{when}: temperature {t.ToString(CultureInfo.InvariantCulture)} °C is not a body temperature in °C.");
        }

        if (Pulse is { } p && (p < 20 || p > 250))
        {
            problems.Add($"{when}: pulse {p} is outside 20–250.");
        }

        if ((SystolicBp is null) != (DiastolicBp is null))
        {
            problems.Add($"{when}: enter both blood pressure numbers, or neither.");
        }
        else if (SystolicBp is { } s && DiastolicBp is { } d && (s < 40 || s > 300 || d < 20 || d > 200 || d >= s))
        {
            problems.Add($"{when}: blood pressure {s}/{d} cannot be right (systolic 40–300, diastolic 20–200, systolic higher).");
        }

        if (RespiratoryRate is { } r && (r < 4 || r > 80))
        {
            problems.Add($"{when}: respiratory rate {r} is outside 4–80.");
        }

        return problems;
    }

    /// <summary>"T 37.0 °C · P 82 · BP 120/80 · RR 16", the signs recorded only.</summary>
    public string Describe()
    {
        var parts = new List<string>();
        if (TemperatureC is { } t) parts.Add($"T {t.ToString("0.0", CultureInfo.InvariantCulture)} °C");
        if (Pulse is { } p) parts.Add($"P {p}");
        if (SystolicBp is { } s && DiastolicBp is { } d) parts.Add($"BP {s}/{d}");
        if (RespiratoryRate is { } r) parts.Add($"RR {r}");
        return string.Join(" · ", parts);
    }

    public string? ToJson() => IsEmpty ? null : JsonSerializer.Serialize(this, Json);

    public static TransfusionVitals? FromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            var vitals = JsonSerializer.Deserialize<TransfusionVitals>(json, Json);
            return vitals is null || vitals.IsEmpty ? null : vitals;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>"Temperature +1.2 °C from before the transfusion", when both temperatures were taken; else null. Stated, not judged.</summary>
    public static string? TemperatureChange(TransfusionVitals? before, TransfusionVitals? after) =>
        before?.TemperatureC is { } b && after?.TemperatureC is { } a
            ? $"Temperature {(a - b >= 0 ? "+" : "−")}{Math.Abs(a - b).ToString("0.0", CultureInfo.InvariantCulture)} °C from before the transfusion"
            : null;
}
