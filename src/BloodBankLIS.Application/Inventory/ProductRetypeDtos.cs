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
    UnitStatus Status)
{
    public static ProductRetypeWorkItemDto From(BloodUnit unit) => new(
        unit.Id,
        unit.UnitNumber,
        unit.ProductType?.ProductCode ?? string.Empty,
        unit.ProductType?.Name ?? string.Empty,
        unit.Abo,
        unit.RhD,
        unit.BloodType.ToString(),
        unit.CreatedUtc,
        unit.Status);
}

public sealed record ProductRetypeSubtestDto(string Code, string Label, bool Required);

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
    string DisplayValue)
{
    public static ProductRetypeResultDto From(ProductRetypeResult r) => new(
        r.Id, r.BloodProductId, r.TestCode, r.InterpretedAbo, r.InterpretedRh,
        r.MatchesLabel, r.DiscrepancyDetail, r.Status, r.EnteredBy, r.EnteredUtc,
        AboRhResultValue.FormatDisplay(r.Value));
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
    string? BlockReason,
    bool AntiDRequired,
    IReadOnlyList<ProductRetypeSubtestDto> Subtests,
    IReadOnlyList<string> GradeChoices,
    ProductRetypeResultDto? Latest);

public sealed record RecordProductRetypeRequest(
    AboGroup InterpretedAbo,
    RhType? InterpretedRh,
    IReadOnlyDictionary<string, string> Subtests);
