namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Staff-facing name for a test choice.
/// The code stays so staff can select the test.
/// A missing or blank name stays the test id and is not replaced by the code.
/// </summary>
public static class TestDefinitionLabel
{
    /// <summary>
    /// Name for a test. A blank name stays the test id.
    /// The test code is not a substitute.
    /// </summary>
    public static string Name(long testId, string? name) =>
        string.IsNullOrWhiteSpace(name) ? $"Test {testId}" : name.Trim();
}
