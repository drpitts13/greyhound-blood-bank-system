namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// The RhIG lot register (end-user testing, 2026-11-12). Receiving needs a lot, the product, an expiry not passed, and the
/// vials; a lot number is registered once. When RhIG is given and the register is in use, the lot must be registered,
/// active, and in date on the day it was given; giving more vials than were received is a warning (the count may be
/// off), not a stop. With no lot registered, the lot is recorded as typed, as before.
/// </summary>
public static class RhigLotRule
{
    public const string ReceiveCode = "RHIG-LOT-RECEIVE";
    public const string UseCode = "RHIG-LOT-USE";

    public static RuleResult EvaluateReceive(string? lot, string? product, DateOnly expiresOn, int vials, DateOnly today, bool lotInUse) =>
        string.IsNullOrWhiteSpace(lot) || string.IsNullOrWhiteSpace(product)
            ? RuleResult.HardStop(ReceiveCode, "Enter the lot number and the product as on the carton.")
            : lotInUse
                ? RuleResult.HardStop(ReceiveCode, $"Lot {lot.Trim()} is already registered.")
                : expiresOn < today
                    ? RuleResult.HardStop(ReceiveCode, $"Lot {lot.Trim()} expired {expiresOn:yyyy-MM-dd}; do not put it into use.")
                    : vials is < 1 or > 1000
                        ? RuleResult.HardStop(ReceiveCode, "Enter the vials received (1–1000).")
                        : RuleResult.Pass(ReceiveCode);

    /// <param name="registerInUse">Any RhIG lot registered (active or not).</param>
    /// <param name="found">The registered lot named, or null.</param>
    /// <param name="vialsUsed">Vials already recorded as given from this lot.</param>
    public static RuleResult EvaluateUse(bool registerInUse, string lot, (bool IsActive, DateOnly ExpiresOn, int VialsReceived)? found,
        DateTime givenUtc, int vials, int vialsUsed, DateOnly? givenOn = null)
    {
        if (!registerInUse)
        {
            return RuleResult.Pass(UseCode);
        }

        if (found is not { } registered)
        {
            return RuleResult.HardStop(UseCode,
                $"Lot {lot.Trim()} is not in the RhIG lot register. Check the vial; a lot just received is registered on RhIG › RhIG lots first.");
        }

        if (!registered.IsActive)
        {
            return RuleResult.HardStop(UseCode, $"Lot {lot.Trim()} is retired from the RhIG lot register; use another lot.");
        }

        // The expiry date is a calendar date at the facility: given that evening is still on the date.
        if ((givenOn ?? DateOnly.FromDateTime(givenUtc)) > registered.ExpiresOn)
        {
            return RuleResult.HardStop(UseCode, $"Lot {lot.Trim()} expired {registered.ExpiresOn:yyyy-MM-dd}, before it was given. Do not give an expired lot.");
        }

        return vialsUsed + vials > registered.VialsReceived
            ? RuleResult.Warning(UseCode,
                $"This makes {vialsUsed + vials} vials given from lot {lot.Trim()}, more than the {registered.VialsReceived} received. Check the count.")
            : RuleResult.Pass(UseCode);
    }
}
