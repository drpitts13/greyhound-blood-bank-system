using System.Text.Json;
using System.Text.Json.Serialization;
using BloodBankLIS.Domain.Reporting;

namespace BloodBankLIS.Application.Reporting;

public static class ReportLayoutJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public static string Serialize(ReportLayout layout) => JsonSerializer.Serialize(layout, Options);

    public static ReportLayout? Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        return JsonSerializer.Deserialize<ReportLayout>(json, Options);
    }
}
