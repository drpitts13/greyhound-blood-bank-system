using BloodBankLIS.Application.Issuing;

namespace BloodBankLIS.Web.Services;

/// <summary>Operator-entered ISBT quadrants for issue or bedside scan verification.</summary>
public sealed class UnitScanModel
{
    public string Din { get; set; } = string.Empty;
    public string ProductCodeData { get; set; } = string.Empty;
    public string ExtendedDivisionCode { get; set; } = string.Empty;
    public string AboRhdCode { get; set; } = string.Empty;
    public string ExpirationEncoded { get; set; } = string.Empty;

    public bool HasAny =>
        !string.IsNullOrWhiteSpace(Din)
        || !string.IsNullOrWhiteSpace(ProductCodeData)
        || !string.IsNullOrWhiteSpace(AboRhdCode)
        || !string.IsNullOrWhiteSpace(ExpirationEncoded)
        || !string.IsNullOrWhiteSpace(ExtendedDivisionCode);

    public ComponentScanVerificationRequest? ToRequest() =>
        HasAny
            ? new ComponentScanVerificationRequest(
                Din.Trim(),
                ProductCodeData.Trim(),
                string.IsNullOrWhiteSpace(ExtendedDivisionCode) ? null : ExtendedDivisionCode.Trim(),
                AboRhdCode.Trim(),
                ExpirationEncoded.Trim())
            : null;

    public void Clear()
    {
        Din = string.Empty;
        ProductCodeData = string.Empty;
        ExtendedDivisionCode = string.Empty;
        AboRhdCode = string.Empty;
        ExpirationEncoded = string.Empty;
    }
}
