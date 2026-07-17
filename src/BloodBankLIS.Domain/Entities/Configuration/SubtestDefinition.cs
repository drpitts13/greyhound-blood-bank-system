using BloodBankLIS.Domain.Common;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Entities.Configuration;

/// <summary>
/// Reusable catalog entry for a panel subtest (e.g. Anti-A). Referenced by
/// <see cref="TestDefinition"/> panel assignments. Graded-reaction choices carry
/// polarity used by interpretation logic tables.
/// </summary>
public class SubtestDefinition : VersionedConfigEntity
{
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public SubtestResultType ResultType { get; set; } = SubtestResultType.GradedReaction;

    /// <summary>JSON array of <see cref="ValueObjects.SubtestChoiceDefinition"/> when applicable.</summary>
    public string? ChoicesJson { get; set; }
}
