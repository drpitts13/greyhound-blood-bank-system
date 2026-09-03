using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Isbt128;

/// <summary>
/// Compares a fresh bedside/issue scan of the four quadrants to the stored canonical component.
/// Any mismatch is a HardStop.
/// </summary>
public static class ComponentScanVerifier
{
    public static RuleEvaluation Verify(
        BloodUnit unit,
        string din,
        string productCodeData,
        string? extendedDivision,
        string aboRhdCode,
        string expirationEncoded)
    {
        var results = new List<RuleResult>();

        results.Add(string.Equals(unit.Din, din, StringComparison.Ordinal)
            ? RuleResult.Pass("SCAN-DIN")
            : RuleResult.HardStop(IsbtErrorCodes.UnitScanMismatch, "Scanned DIN does not match stored component."));

        results.Add(string.Equals(unit.ProductCodeData, productCodeData, StringComparison.Ordinal)
            ? RuleResult.Pass("SCAN-PRODUCT")
            : RuleResult.HardStop(IsbtErrorCodes.UnitScanMismatch, "Scanned product code/division does not match stored component."));

        var unitExt = unit.ExtendedDivisionCode ?? string.Empty;
        var scanExt = extendedDivision ?? string.Empty;
        results.Add(string.Equals(unitExt, scanExt, StringComparison.Ordinal)
            ? RuleResult.Pass("SCAN-EXT-DIV")
            : RuleResult.HardStop(IsbtErrorCodes.UnitScanMismatch, "Scanned extended division does not match stored component."));

        results.Add(string.Equals(unit.AboRhdCode, aboRhdCode, StringComparison.Ordinal)
            ? RuleResult.Pass("SCAN-ABO")
            : RuleResult.HardStop(IsbtErrorCodes.UnitScanMismatch, "Scanned ABO/RhD does not match stored component."));

        results.Add(string.Equals(unit.ExpirationEncoded, expirationEncoded, StringComparison.Ordinal)
            ? RuleResult.Pass("SCAN-EXP")
            : RuleResult.HardStop(IsbtErrorCodes.UnitScanMismatch, "Scanned expiration does not match stored component."));

        if (unit.Status == Enums.UnitStatus.Recalled)
            results.Add(RuleResult.HardStop(IsbtErrorCodes.ComponentRecalled, "Component is recalled."));
        if (unit.Status == Enums.UnitStatus.Quarantine)
            results.Add(RuleResult.HardStop(IsbtErrorCodes.ComponentQuarantined, "Component is quarantined."));
        if (unit.Status == Enums.UnitStatus.OnHold)
            results.Add(RuleResult.HardStop(IsbtErrorCodes.ComponentOnHold, "Component is on operational hold."));
        if (unit.Status is Enums.UnitStatus.Transfused or Enums.UnitStatus.TransfusionStarted)
            results.Add(RuleResult.HardStop(IsbtErrorCodes.ComponentAlreadyTransfused, "Component transfusion already started or completed."));

        return new RuleEvaluation(results);
    }
}
