using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BloodBankLIS.Domain.ValueObjects;

/// <summary>
/// JSON options for clinical text (reaction grades such as <c>4+</c>).
/// The default encoder writes <c>+</c> as <c>\u002B</c>; relaxed escaping keeps the plus readable.
/// </summary>
public static class ClinicalJson
{
    public static JsonSerializerOptions Options { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter() }
    };

    public static JsonSerializerOptions WebOptions { get; } = new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };
}
