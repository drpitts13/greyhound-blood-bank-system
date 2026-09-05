using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.ValueObjects;

/// <summary>Configurable exclusion thresholds for antibody-identification assistance.</summary>
public sealed record AntibodyIdentificationPolicy(
    bool DosageAware,
    int MinHomozygousExclusions,
    int MinHeterozygousExclusions,
    bool RequireSupervisorReview,
    bool BlockSelfReview,
    IReadOnlySet<string> DosageSensitiveCodes)
{
    public static IReadOnlySet<string> DefaultDosageSensitiveCodes { get; } =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "C", "c", "E", "e", "FYA", "FYB", "JKA", "JKB", "M", "N", "S", "s"
        };

    public static AntibodyIdentificationPolicy Default { get; } = new(
        DosageAware: true,
        MinHomozygousExclusions: 1,
        MinHeterozygousExclusions: 2,
        RequireSupervisorReview: true,
        BlockSelfReview: true,
        DosageSensitiveCodes: DefaultDosageSensitiveCodes);

    public bool IsDosageSensitive(string antigenCode) =>
        DosageAware && DosageSensitiveCodes.Contains(antigenCode);
}

/// <summary>Catalog antigen considered during assistance.</summary>
public sealed record AntibodyIdAntigenInfo(string Code, string AntibodyName);

/// <summary>One reagent cell with antigen typings and recorded phase reactions.</summary>
public sealed record AntibodyIdentificationCellSnapshot(
    string CellKey,
    string CellNumber,
    PanelCellRole Role,
    IReadOnlyDictionary<string, AntigenExpression> Antigens,
    IReadOnlyDictionary<string, ReactionGrade> Reactions);

/// <summary>Patient phenotype or predicted genotype used only as an advisory comparison.</summary>
public sealed record PatientAntigenSnapshot(
    string AttributeCode,
    AntigenResult Result,
    bool FromGenotype);

/// <summary>Historical antibody that must remain visible during identification.</summary>
public sealed record HistoricalAntibodySnapshot(
    string Specificity,
    string? AttributeCode,
    AntibodyStatus Status,
    bool IsActive);

/// <summary>Inputs to the advisory antibody-identification engine.</summary>
public sealed record AntibodyIdentificationAssistInput(
    IReadOnlyList<AntibodyIdentificationCellSnapshot> Cells,
    IReadOnlyList<string> InterpretivePhases,
    IReadOnlyList<AntibodyIdAntigenInfo> Antigens,
    IReadOnlyList<PatientAntigenSnapshot> PatientAntigens,
    IReadOnlyList<HistoricalAntibodySnapshot> HistoricalAntibodies,
    AntibodyIdDatResult Dat,
    AntibodyIdentificationPolicy Policy);

/// <summary>One advisory finding. Classification is never <see cref="AntibodyIdClassification.Identified"/>.</summary>
public sealed record AntibodyIdentificationAssistFinding(
    string Specificity,
    string? AttributeCode,
    AntibodyIdClassification Classification,
    string Rationale,
    int HomozygousExclusions,
    int HeterozygousExclusions,
    int ConcordantPositives,
    int DiscordantPositives);

/// <summary>Advisory evaluation. Findings assist interpretation; they do not identify antibodies.</summary>
public sealed record AntibodyIdentificationAssistResult(
    IReadOnlyList<AntibodyIdentificationAssistFinding> Findings,
    RuleEvaluation Evaluation);

/// <summary>Technologist or supervisor finding recorded on a workup.</summary>
public sealed record AntibodyIdentificationRecordedFinding(
    string Specificity,
    string? AttributeCode,
    AntibodyIdClassification Classification,
    AntibodyIdSource Source);

public static class ReactionGradeInfo
{
    public static bool IsPositive(ReactionGrade grade) =>
        grade is ReactionGrade.Weak or ReactionGrade.OnePlus or ReactionGrade.TwoPlus
            or ReactionGrade.ThreePlus or ReactionGrade.FourPlus or ReactionGrade.Microscopic
            or ReactionGrade.Hemolysis or ReactionGrade.MixedField;

    public static bool IsNegative(ReactionGrade grade) => grade == ReactionGrade.Negative;

    public static bool IsComplete(ReactionGrade grade) =>
        grade is not ReactionGrade.NotTested and not ReactionGrade.Invalid;
}

public static class AntigenExpressionInfo
{
    public static bool IsPresent(AntigenExpression expression) =>
        expression is AntigenExpression.Present or AntigenExpression.Heterozygous
            or AntigenExpression.Homozygous or AntigenExpression.Weak;

    public static bool IsAbsent(AntigenExpression expression) => expression == AntigenExpression.Absent;

    public static bool IsHeterozygousDose(AntigenExpression expression) =>
        expression is AntigenExpression.Heterozygous or AntigenExpression.Weak;
}
