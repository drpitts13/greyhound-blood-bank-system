namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Staff-facing name for a special requirement in the catalog.
/// The code stays so staff can select the requirement.
/// A missing or blank name stays the requirement id and is not replaced by the code.
/// </summary>
public static class SpecialRequirementLabel
{
    /// <summary>
    /// Name for a special requirement. A blank name stays the requirement id.
    /// The requirement code and a product-attribute code are not substitutes.
    /// </summary>
    public static string Name(long requirementId, string? name) =>
        string.IsNullOrWhiteSpace(name) ? $"Requirement {requirementId}" : name.Trim();
}
