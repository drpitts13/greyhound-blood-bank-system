namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Chart-header copy for an electronic crossmatch that is already not eligible.
/// Shows the existing blocking reason. Does not decide eligibility.
/// </summary>
public static class ElectronicCrossmatchBanner
{
    public static string? Notice(bool eligible, string? blockingReason)
    {
        if (eligible || string.IsNullOrWhiteSpace(blockingReason))
        {
            return null;
        }

        return blockingReason.Trim();
    }
}
