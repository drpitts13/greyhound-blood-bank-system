using System.Text.Json;
using System.Text.Json.Serialization;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.ValueObjects;

public sealed record SubtestChoiceDefinition(
    string Code,
    string Label,
    ReactionPolarity? Polarity);

public static class SubtestChoiceDefinitions
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    public static string? ToJson(IReadOnlyList<SubtestChoiceDefinition>? items)
    {
        if (items is null || items.Count == 0)
        {
            return null;
        }

        return JsonSerializer.Serialize(items, JsonOptions);
    }

    public static IReadOnlyList<SubtestChoiceDefinition> Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Array.Empty<SubtestChoiceDefinition>();
        }

        try
        {
            var items = JsonSerializer.Deserialize<List<SubtestChoiceDefinition>>(json, JsonOptions);
            return items is null || items.Count == 0
                ? Array.Empty<SubtestChoiceDefinition>()
                : items;
        }
        catch (JsonException)
        {
            return Array.Empty<SubtestChoiceDefinition>();
        }
    }

    /// <summary>Standard graded-reaction choices mapped from <see cref="CellReactionGradeValue"/>.</summary>
    public static IReadOnlyList<SubtestChoiceDefinition> DefaultGradedReaction() =>
    [
        new("0", "0", ReactionPolarity.Negative),
        new("1+", "1+", ReactionPolarity.Positive),
        new("2+", "2+", ReactionPolarity.Positive),
        new("3+", "3+", ReactionPolarity.Positive),
        new("4+", "4+", ReactionPolarity.Positive),
        new("H", "Hemolysis", ReactionPolarity.Positive),
        new("w+", "Weak positive", ReactionPolarity.Positive),
        new("+/-", "Mixed", ReactionPolarity.Neutral),
        new("NT", "Not tested", ReactionPolarity.Neutral)
    ];
}
