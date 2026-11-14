using System.Text.Json;

namespace BloodBankLIS.Domain.ValueObjects;

/// <summary>One dilution of a titer and the grade read there (1:<see cref="Dilution"/>).</summary>
public sealed record TiterReading(int Dilution, string Grade);

/// <summary>
/// A titer result as stored in <c>TestResult.Value</c>: the phase read, every dilution's grade, the endpoint dilution
/// (null when no dilution reached the endpoint grade), and the score when the test is scored.
/// </summary>
public sealed record TiterResultValue(
    string? PhaseCode,
    int StartDilution,
    IReadOnlyList<TiterReading> Readings,
    int? Endpoint,
    int? Score)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>"1:32", or "&lt;1:1" when no dilution reached the endpoint grade.</summary>
    public string EndpointLabel => Endpoint is int endpoint ? $"1:{endpoint}" : $"<1:{StartDilution}";

    /// <summary>"Titer 1:32" or "Titer 1:32 (score 45)".</summary>
    public string Interpretation => Score is int score ? $"Titer {EndpointLabel} (score {score})" : $"Titer {EndpointLabel}";

    public string Serialize() => JsonSerializer.Serialize(this, Json);

    public static bool TryParse(string? json, out TiterResultValue? value)
    {
        value = null;
        if (string.IsNullOrWhiteSpace(json) || !json.TrimStart().StartsWith('{'))
        {
            return false;
        }

        try
        {
            value = JsonSerializer.Deserialize<TiterResultValue>(json, Json);
            return value is not null && value.StartDilution > 0;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
