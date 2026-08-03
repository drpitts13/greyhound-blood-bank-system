namespace BloodBankLIS.Domain.Isbt128;

/// <summary>
/// Builds the canonical component identity from DIN + full product data
/// (+ extended division when required). Never use DIN alone as identity —
/// multiple components/divisions may originate from the same donation.
/// </summary>
public static class ComponentIdentityBuilder
{
    public const char Separator = '|';

    public static string Build(string din, string productCodeData, string? extendedDivisionCode = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(din);
        ArgumentException.ThrowIfNullOrWhiteSpace(productCodeData);

        var identity = din.Trim() + Separator + productCodeData.Trim();
        if (!string.IsNullOrWhiteSpace(extendedDivisionCode))
            identity += Separator + extendedDivisionCode.Trim();

        return identity;
    }

    /// <summary>Persisted uniqueness key: empty extended division coalesced to empty string.</summary>
    public static string BuildUniquenessKey(string din, string productCodeData, string? extendedDivisionCode) =>
        Build(din, productCodeData, string.IsNullOrWhiteSpace(extendedDivisionCode) ? null : extendedDivisionCode);
}
