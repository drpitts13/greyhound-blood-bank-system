using BloodBankLIS.Application.Admin;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Application.Reference;

/// <summary>Active test definition fields needed for clinical result entry UI.</summary>
public sealed record TestDefinitionForEntryDto(
    string Code,
    string Name,
    ResultValueType ResultValueType,
    string? AllowedResultValues,
    IReadOnlyList<ResolvedPanelSubtestDto> PanelSubtests,
    IReadOnlyList<InterpretationOptionDto> InterpretationOptions,
    IReadOnlyList<BloodAttributeListItemDto> BloodAttributeScope,
    BloodAttributeKind? BloodAttributeScopeKind,
    bool ContributesToUnitBloodAttributes);

public sealed record InterpretationOptionDto(string Key, string Label);

public sealed record ResolvedPanelSubtestDto(
    string SubtestCode,
    string Label,
    SubtestResultType ResultType,
    IReadOnlyList<SubtestChoiceDto> Choices,
    bool Required,
    int SortOrder);

public sealed record SubtestListItemDto(string Code, string Name, SubtestResultType ResultType);

public sealed record BloodAttributeListItemDto(
    long Id,
    string Code,
    string Name,
    string AntibodyName,
    bool IsClinicallySignificant,
    int SortOrder);

public sealed record TestGrouperListItemDto(
    string Code,
    string Name,
    IReadOnlyList<string> MemberTestCodes);
