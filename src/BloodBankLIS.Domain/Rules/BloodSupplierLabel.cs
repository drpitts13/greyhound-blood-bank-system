namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Staff-facing label for a blood supplier choice.
/// The code stays so staff can select the supplier.
/// A missing or blank name stays the supplier id and is not replaced by the code.
/// </summary>
public static class BloodSupplierLabel
{
    public static string ForChoice(long supplierId, string? code, string? name)
    {
        var place = string.IsNullOrWhiteSpace(name) ? $"Supplier {supplierId}" : name.Trim();
        if (string.IsNullOrWhiteSpace(code))
        {
            return place;
        }

        return $"{code.Trim()} — {place}";
    }
}
