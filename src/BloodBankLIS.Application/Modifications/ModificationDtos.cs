using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Application.Modifications;

/// <summary>An active <c>ModificationRule</c> applicable to a specific unit's current product code.</summary>
public sealed record EligibleModificationDto(
    long RuleId,
    string ModificationCode,
    ModificationType ModificationType,
    long TargetProductTypeId,
    string TargetProductCode,
    string ExpirationOffsetCode,
    ExpirationRelativeTo ExpirationRelativeTo,
    string? Description,
    DateTime? PreviewExpiresUtc,
    bool RequiresCollectionDate,
    bool IsAvailable);

/// <summary>One requested result unit of a Divide (an operator-chosen unit-number suffix and/or volume).</summary>
public sealed record DivideChildSpec(string? UnitNumberSuffix, decimal? Volume);

public sealed record PerformDivideRequest(long RuleId, IReadOnlyList<DivideChildSpec> Children, string Reason);

public sealed record PerformPoolRequest(IReadOnlyList<long> SourceUnitIds, long RuleId, string Reason);

/// <summary>Covers the 1-source/1-result modification types: Irradiate, Thaw, Volume Reduction, Leukoreduction.</summary>
public sealed record PerformSingleModificationRequest(long RuleId, decimal? ResultVolume, string Reason);

public sealed record ModificationUnitSummaryDto(long UnitId, string UnitNumber, ModificationUnitRole Role);

public sealed record UnitModificationDto(
    long Id,
    ModificationType ModificationType,
    string SourceProductCode,
    string TargetProductCode,
    string ExpirationOffsetCodeApplied,
    DateTime ResultExpiresUtc,
    string Reason,
    string PerformedBy,
    DateTime PerformedUtc,
    IReadOnlyList<ModificationUnitSummaryDto> Units);
