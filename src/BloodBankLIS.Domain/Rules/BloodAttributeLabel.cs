namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Staff-facing names for a blood attribute in the catalog.
/// The code stays so staff can select the attribute.
/// A missing or blank name stays the attribute id and is not replaced by the code.
/// A missing or blank antibody name stays the same id and is not replaced by the antigen name or the code.
/// </summary>
public static class BloodAttributeLabel
{
    /// <summary>
    /// Antigen name. A blank name stays the attribute id.
    /// The attribute code is not a substitute.
    /// </summary>
    public static string Name(long attributeId, string? name) =>
        string.IsNullOrWhiteSpace(name) ? $"Attribute {attributeId}" : name.Trim();

    /// <summary>
    /// Antibody name. A blank name stays the attribute id.
    /// The antigen name and the attribute code are not substitutes.
    /// </summary>
    public static string Antibody(long attributeId, string? antibodyName) =>
        string.IsNullOrWhiteSpace(antibodyName) ? $"Antibody {attributeId}" : antibodyName.Trim();
}
