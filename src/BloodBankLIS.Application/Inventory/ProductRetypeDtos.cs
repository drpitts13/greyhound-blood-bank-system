using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.ValueObjects;

namespace BloodBankLIS.Application.Inventory;

public sealed record ProductRetypeWorkItemDto(
    long UnitId,
    string UnitNumber,
    string ProductCode,
    string ProductName,
    AboGroup Abo,
    RhType RhD,
    string BloodType,
    DateTime ReceivedUtc,
    UnitStatus Status,
    string? TestCode = null,
    string? TestName = null,
    ResultStatus? ResultStatus = null)
{
    public static ProductRetypeWorkItemDto From(
        BloodUnit unit,
        ProductRetypeResult? latest = null,
        string? testName = null) => new(
        unit.Id,
        unit.UnitNumber,
        unit.ProductType?.ProductCode ?? string.Empty,
        unit.ProductType?.Name ?? string.Empty,
        unit.Abo,
        unit.RhD,
        unit.BloodType.ToString(),
        unit.CreatedUtc,
        unit.Status,
        latest?.TestCode,
        testName,
        latest?.Status);
}

public sealed record ProductRetypeSubtestDto(string Code, string Label, bool Required);

public sealed record ProductRetypeCellReactionDto(string Code, string Label, string Grade);

public sealed record ProductRetypeResultDto(
    long Id,
    long BloodProductId,
    string TestCode,
    AboGroup InterpretedAbo,
    RhType? InterpretedRh,
    bool MatchesLabel,
    string? DiscrepancyDetail,
    ResultStatus Status,
    string EnteredBy,
    DateTime EnteredUtc,
    string? VerifiedBy,
    string DisplayValue,
    IReadOnlyList<ProductRetypeCellReactionDto> CellReactions)
{
    public static ProductRetypeResultDto From(ProductRetypeResult r) => new(
        r.Id, r.BloodProductId, r.TestCode, r.InterpretedAbo, r.InterpretedRh,
        r.MatchesLabel, r.DiscrepancyDetail, r.Status, r.EnteredBy, r.EnteredUtc,
        r.VerifiedBy, AboRhResultValue.FormatDisplay(r.Value), CellReactionsOf(r));

    private static IReadOnlyList<ProductRetypeCellReactionDto> CellReactionsOf(ProductRetypeResult r)
    {
        if (!AboRhResultValue.TryParsePanel(r.Value, out var panel))
        {
            return [];
        }

        var labels = PanelSubtestDefinitions.DefaultAboRh()
            .Concat(PanelSubtestDefinitions.DefaultAboRhRetype())
            .Concat(PanelSubtestDefinitions.DefaultAboRhRetypePositive())
            .Concat(PanelSubtestDefinitions.DefaultAboRhRetypeNegative())
            .GroupBy(s => s.Code, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Label, StringComparer.OrdinalIgnoreCase);
        var order = AboRhPanelSubtestCodes.All
            .Select((code, index) => (code, index))
            .ToDictionary(x => x.code, x => x.index, StringComparer.OrdinalIgnoreCase);

        return panel.Subtests
            .Where(kv => !string.IsNullOrWhiteSpace(kv.Value)
                && !string.Equals(kv.Value, "NT", StringComparison.OrdinalIgnoreCase))
            .OrderBy(kv => order.TryGetValue(kv.Key, out var index) ? index : int.MaxValue)
            .ThenBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
            .Select(kv => new ProductRetypeCellReactionDto(
                kv.Key,
                labels.TryGetValue(kv.Key, out var label) ? label : kv.Key,
                kv.Value))
            .ToList();
    }
}

public sealed record ProductRetypeDetailDto(
    long UnitId,
    string UnitNumber,
    string ProductCode,
    string ProductName,
    bool RequiresRetype,
    AboGroup LabeledAbo,
    RhType LabeledRh,
    string LabeledBloodType,
    UnitStatus Status,
    bool CanRecord,
    bool CanVerify,
    string? BlockReason,
    bool AntiDRequired,
    IReadOnlyList<ProductRetypeSubtestDto> Subtests,
    IReadOnlyList<string> GradeChoices,
    ProductRetypeResultDto? Latest,
    string? TestCode = null,
    string? TestName = null);

public sealed record RecordProductRetypeRequest(
    AboGroup InterpretedAbo,
    RhType? InterpretedRh,
    IReadOnlyDictionary<string, string> Subtests);
