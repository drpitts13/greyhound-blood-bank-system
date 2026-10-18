namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Staff-facing name for a subtest in the catalog.
/// The code stays so staff can select the subtest.
/// A missing or blank name stays the subtest id and is not replaced by the code.
/// </summary>
public static class SubtestDefinitionLabel
{
    /// <summary>
    /// Name for a subtest. A blank name stays the subtest id.
    /// The subtest code is not a substitute.
    /// </summary>
    public static string Name(long subtestId, string? name) =>
        string.IsNullOrWhiteSpace(name) ? $"Subtest {subtestId}" : name.Trim();
}
