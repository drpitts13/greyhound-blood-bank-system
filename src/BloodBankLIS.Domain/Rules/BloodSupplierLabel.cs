namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Staff-facing label for a blood supplier choice.
/// The code stays so staff can select the supplier.
/// A missing or blank name stays the supplier id and is not replaced by the code.
/// </summary>
public static class BloodSupplierLabel
{
    /// <summary>
    /// Name for the supplier catalog. A blank name stays the supplier id.
    /// The supplier code is not a substitute.
    /// </summary>
    public static string Name(long supplierId, string? name) =>
        string.IsNullOrWhiteSpace(name) ? $"Supplier {supplierId}" : name.Trim();

    public static string ForChoice(long supplierId, string? code, string? name)
    {
        var place = Name(supplierId, name);
        if (string.IsNullOrWhiteSpace(code))
        {
            return place;
        }

        return $"{code.Trim()} — {place}";
    }
}
