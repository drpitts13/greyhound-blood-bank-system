using System.Text.Json;
using System.Text.Json.Serialization;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.ValueObjects;

/// <summary>References a catalog <see cref="SubtestDefinition"/> on a panel test.</summary>
public sealed record PanelSubtestAssignment(
    string SubtestCode,
    bool Required,
    int SortOrder = 0,
    IReadOnlyList<string>? PhaseCodes = null);

/// <summary>Resolved phase for result entry (catalog joined with assignment).</summary>
public sealed record ResolvedPanelPhase(
    string PhaseCode,
    string Label,
    bool Required,
    bool IncludeInInterpretation,
    bool IsCheckCell,
    string? ValidatesPhaseCode,
    int SortOrder);

/// <summary>Resolved subtest for result entry (catalog joined with assignment).</summary>
public sealed record ResolvedPanelSubtest(
    string SubtestCode,
    string Label,
    SubtestResultType ResultType,
    IReadOnlyList<SubtestChoiceDefinition> Choices,
    bool Required,
    int SortOrder,
    IReadOnlyList<ResolvedPanelPhase>? Phases = null);

public static class PanelSubtestAssignments
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static string? ToJson(IReadOnlyList<PanelSubtestAssignment>? items)
    {
        if (items is null || items.Count == 0)
        {
            return null;
        }

        return JsonSerializer.Serialize(items.OrderBy(i => i.SortOrder).ThenBy(i => i.SubtestCode), JsonOptions);
    }

    public static IReadOnlyList<PanelSubtestAssignment> Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Array.Empty<PanelSubtestAssignment>();
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<PanelSubtestAssignment>();
            }

            var results = new List<PanelSubtestAssignment>();
            foreach (var el in doc.RootElement.EnumerateArray())
            {
                if (el.TryGetProperty("subtestCode", out var subtestCodeProp))
                {
                    var code = subtestCodeProp.GetString() ?? string.Empty;
                    var required = el.TryGetProperty("required", out var req) && req.GetBoolean();
                    var sort = el.TryGetProperty("sortOrder", out var so) ? so.GetInt32() : 0;
                    results.Add(new PanelSubtestAssignment(code, required, sort, ReadPhaseCodes(el)));
                }
                else if (el.TryGetProperty("code", out var codeProp))
                {
                    var code = codeProp.GetString() ?? string.Empty;
                    var required = el.TryGetProperty("required", out var req) && req.GetBoolean();
                    var sort = el.TryGetProperty("sortOrder", out var so) ? so.GetInt32() : 0;
                    results.Add(new PanelSubtestAssignment(code, required, sort, ReadPhaseCodes(el)));
                }
            }

            return results.Count == 0
                ? Array.Empty<PanelSubtestAssignment>()
                : results.OrderBy(i => i.SortOrder).ThenBy(i => i.SubtestCode).ToList();
        }
        catch (JsonException)
        {
            return Array.Empty<PanelSubtestAssignment>();
        }
    }

    private static IReadOnlyList<string>? ReadPhaseCodes(JsonElement el)
    {
        if (!el.TryGetProperty("phaseCodes", out var phases) || phases.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var codes = phases.EnumerateArray()
            .Select(p => p.GetString()?.Trim() ?? string.Empty)
            .Where(c => c.Length > 0)
            .ToList();
        return codes.Count == 0 ? null : codes;
    }

    public static IReadOnlyList<ResolvedPanelSubtest> ResolveForEntry(
        string? panelSubtestsJson,
        IReadOnlyDictionary<string, SubtestDefinition> catalogByCode,
        bool useAboRhDefaultsWhenEmpty,
        IReadOnlyDictionary<string, PhaseDefinition>? phasesByCode = null)
    {
        var assignments = Parse(panelSubtestsJson);
        if (assignments.Count == 0 && useAboRhDefaultsWhenEmpty)
        {
            assignments = PanelSubtestDefinitions.DefaultAboRh()
                .Select(s => new PanelSubtestAssignment(s.Code, s.Required, s.SortOrder))
                .ToList();
        }

        if (assignments.Count == 0)
        {
            return Array.Empty<ResolvedPanelSubtest>();
        }

        var resolved = new List<ResolvedPanelSubtest>();
        foreach (var a in assignments)
        {
            if (!catalogByCode.TryGetValue(a.SubtestCode.Trim(), out var def))
            {
                resolved.Add(new ResolvedPanelSubtest(
                    a.SubtestCode,
                    a.SubtestCode,
                    SubtestResultType.GradedReaction,
                    SubtestChoiceDefinitions.DefaultGradedReaction(),
                    a.Required,
                    a.SortOrder,
                    ResolvePhases(a, phasesByCode)));
                continue;
            }

            var choices = def.ResultType == SubtestResultType.FreeText
                ? Array.Empty<SubtestChoiceDefinition>()
                : SubtestChoiceDefinitions.Parse(def.ChoicesJson);

            if (def.ResultType == SubtestResultType.GradedReaction && choices.Count == 0)
            {
                choices = SubtestChoiceDefinitions.DefaultGradedReaction();
            }

            resolved.Add(new ResolvedPanelSubtest(
                def.Code,
                def.Name,
                def.ResultType,
                choices,
                a.Required,
                a.SortOrder,
                ResolvePhases(a, phasesByCode)));
        }

        return resolved.OrderBy(r => r.SortOrder).ThenBy(r => r.SubtestCode).ToList();
    }

    private static IReadOnlyList<ResolvedPanelPhase> ResolvePhases(
        PanelSubtestAssignment assignment,
        IReadOnlyDictionary<string, PhaseDefinition>? phasesByCode)
    {
        if (assignment.PhaseCodes is not { Count: > 0 })
        {
            return Array.Empty<ResolvedPanelPhase>();
        }

        var resolved = new List<ResolvedPanelPhase>();
        var order = 0;
        foreach (var raw in assignment.PhaseCodes)
        {
            var code = raw.Trim();
            if (code.Length == 0)
            {
                continue;
            }

            order++;
            if (phasesByCode is not null && phasesByCode.TryGetValue(code, out var def))
            {
                resolved.Add(new ResolvedPanelPhase(
                    def.Code,
                    def.Name,
                    Required: assignment.Required && def.IncludeInInterpretation && !def.IsCheckCell,
                    def.IncludeInInterpretation,
                    def.IsCheckCell,
                    def.ValidatesPhaseCode,
                    def.SortOrder != 0 ? def.SortOrder : order));
                continue;
            }

            resolved.Add(new ResolvedPanelPhase(
                code,
                code,
                Required: assignment.Required,
                IncludeInInterpretation: true,
                IsCheckCell: false,
                ValidatesPhaseCode: null,
                order));
        }

        return resolved.OrderBy(p => p.SortOrder).ThenBy(p => p.PhaseCode).ToList();
    }
}
