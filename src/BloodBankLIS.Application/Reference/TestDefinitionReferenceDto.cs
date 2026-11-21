using BloodBankLIS.Application.Admin;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

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
    bool ContributesToUnitBloodAttributes,
    bool ContributesToAntibodyHistory = false,
    // Numeric tests: units and limits, so entry shows the range and flags as the value is typed.
    string? ResultUnits = null,
    NumericLimits? Numeric = null,
    // Titer tests: the dilution series, phase, endpoint grade, and scoring.
    TiterSettings? Titer = null);

/// <summary>
/// An interpretation the result can be given, with the reaction pattern that implies it
/// so entry can fill the interpretation in from the entered reactions.
/// </summary>
public sealed record InterpretationOptionDto(
    string Key,
    string Label,
    IReadOnlyDictionary<string, ReactionPolarity>? SubtestExpectations = null,
    InterpretationMatchMode MatchMode = InterpretationMatchMode.AllMatch);

public sealed record ResolvedPanelPhaseDto(
    string PhaseCode,
    string Label,
    bool Required,
    bool IncludeInInterpretation,
    bool IsCheckCell,
    string? ValidatesPhaseCode,
    int SortOrder);

public sealed record ResolvedPanelSubtestDto(
    string SubtestCode,
    string Label,
    SubtestResultType ResultType,
    IReadOnlyList<SubtestChoiceDto> Choices,
    bool Required,
    int SortOrder,
    IReadOnlyList<ResolvedPanelPhaseDto>? Phases = null,
    // False for a check-cells reading entered as its own subtest: the suggestion does not count it.
    bool CountsInInterpretation = true);

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
