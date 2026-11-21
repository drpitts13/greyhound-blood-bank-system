namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// The volume asked for on a product line (end-user testing pass 118: a product order could state only whole units, so
/// "15 mL of red cells" for a neonate could not be ordered and the blood bank prepared an aliquot from a phone call).
/// A whole number of mL from 1 to <see cref="MaxMl"/>; none means a whole unit.
/// </summary>
public static class RequestedVolumeRule
{
    public const int MaxMl = 1000;

    public static string? Validate(int? volumeMl) =>
        volumeMl is int ml && (ml < 1 || ml > MaxMl)
            ? $"A requested volume must be between 1 and {MaxMl} mL."
            : null;

    /// <summary>"Red Blood Cells (15 mL)", or the name alone for a whole unit.</summary>
    public static string Label(string productName, int? volumeMl) =>
        volumeMl is int ml ? $"{productName} ({ml} mL)" : productName;
}
