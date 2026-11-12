using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// The device a modification was performed on (irradiator, cell washer, thawer) and, for irradiation, the dose
/// indicator check. An indicator reported as not confirmed never records the unit as irradiated. Whether the irradiator
/// and the indicator check are required is a facility policy (OCD-067), off by default.
/// </summary>
public static class ModificationDeviceRule
{
    public const string Code = "MOD-DEVICE";

    public static RuleResult Evaluate(ModificationType type, bool irradiationRecordRequired, string? deviceId, bool? indicatorConfirmed)
    {
        if (type != ModificationType.Irradiate)
        {
            return RuleResult.Pass(Code);
        }

        if (indicatorConfirmed == false)
        {
            return RuleResult.HardStop(Code,
                "The dose indicator did not confirm irradiation, so the unit is not recorded as irradiated. Follow the irradiation SOP for this unit.");
        }

        if (!irradiationRecordRequired)
        {
            return RuleResult.Pass(Code);
        }

        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return RuleResult.HardStop(Code, "Scan or type the irradiator used.");
        }

        return indicatorConfirmed == true
            ? RuleResult.Pass(Code)
            : RuleResult.HardStop(Code, "Check the dose indicator on each unit and confirm it shows irradiated.");
    }

    public const string OutOfServiceCode = "MOD-DEVICE-OOS";

    /// <summary>
    /// A device that is registered equipment (Cycle 709) must be in service: equipment taken out of service, or retired
    /// from the register, is refused for a modification. A device not in the register is recorded as typed.
    /// </summary>
    public static RuleResult EvaluateRegistered(string code, bool isActive, DateTime? outOfServiceUtc, string? outOfServiceReason) =>
        !isActive
            ? RuleResult.HardStop(OutOfServiceCode, $"{code} is retired from the equipment register. Use equipment in service.")
            : outOfServiceUtc is not null
                ? RuleResult.HardStop(OutOfServiceCode,
                    $"{code} is out of service since {outOfServiceUtc.Value:yyyy-MM-dd}{(string.IsNullOrWhiteSpace(outOfServiceReason) ? "" : $" ({outOfServiceReason.Trim()})")}. Use equipment in service, or return it to service on Equipment once it may be used.")
                : RuleResult.Pass(OutOfServiceCode);

    /// <summary>"IRR-1, indicator confirmed", "WASHER-2", or null when nothing was recorded.</summary>
    public static string? Describe(string? deviceId, bool? indicatorConfirmed)
    {
        var device = string.IsNullOrWhiteSpace(deviceId) ? null : deviceId.Trim();
        var indicator = indicatorConfirmed switch
        {
            true => "indicator confirmed",
            false => "indicator not confirmed",
            null => null
        };
        return (device, indicator) switch
        {
            (null, null) => null,
            (null, _) => indicator,
            (_, null) => device,
            _ => $"{device}, {indicator}"
        };
    }
}
