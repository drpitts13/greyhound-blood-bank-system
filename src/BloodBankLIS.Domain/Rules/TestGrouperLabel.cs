namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Staff-facing name for a test grouper in the catalog.
/// The code stays so staff can select the grouper.
/// A missing or blank name stays the grouper id and is not replaced by the code.
/// </summary>
public static class TestGrouperLabel
{
    /// <summary>
    /// Name for a test grouper. A blank name stays the grouper id.
    /// The grouper code is not a substitute.
    /// </summary>
    public static string Name(long grouperId, string? name) =>
        string.IsNullOrWhiteSpace(name) ? $"Grouper {grouperId}" : name.Trim();
}
