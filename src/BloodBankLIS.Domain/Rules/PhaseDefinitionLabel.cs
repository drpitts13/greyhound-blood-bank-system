namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Staff-facing name for a reaction phase in the catalog.
/// The code stays so staff can select the phase.
/// A missing or blank name stays the phase id and is not replaced by the code.
/// </summary>
public static class PhaseDefinitionLabel
{
    /// <summary>
    /// Name for a phase. A blank name stays the phase id.
    /// The phase code is not a substitute.
    /// </summary>
    public static string Name(long phaseId, string? name) =>
        string.IsNullOrWhiteSpace(name) ? $"Phase {phaseId}" : name.Trim();
}
