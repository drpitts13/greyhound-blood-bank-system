namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Staff-facing label for a product choice.
/// The code stays so staff can select the product.
/// A missing or blank name stays the product id and is not replaced by the code.
/// </summary>
public static class ProductTypeLabel
{
    /// <summary>
    /// Name for a product. A blank name stays the product id.
    /// The product code is not a substitute.
    /// </summary>
    public static string Name(long productId, string? name) =>
        string.IsNullOrWhiteSpace(name) ? $"Product {productId}" : name.Trim();

    public static string ForChoice(long productId, string? code, string? name)
    {
        var place = Name(productId, name);
        if (string.IsNullOrWhiteSpace(code))
        {
            return place;
        }

        return $"{code.Trim()} — {place}";
    }
}
