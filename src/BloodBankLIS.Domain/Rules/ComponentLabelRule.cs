using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// When a component (ISBT 128) label may be printed or reprinted (end-user testing pass 102: a label printed for any unit,
/// expired, discarded, transfused or shipped away, so a fresh label could be put on a unit that must not be used). A label
/// is not printed for a unit past its expiration or no longer in the blood bank's stock; a quarantined or held unit is
/// still labelled, since labelling often comes before release.
/// </summary>
public static class ComponentLabelRule
{
    public const string Code = "LABEL-UNIT-STATE";

    private static readonly HashSet<UnitStatus> NotLabelled =
    [
        UnitStatus.Expired, UnitStatus.Discarded, UnitStatus.Transfused, UnitStatus.Modified, UnitStatus.ShippedOut,
        UnitStatus.ReturnedToSupplier, UnitStatus.Transferred, UnitStatus.Missing
    ];

    public static RuleResult Evaluate(string unitNumber, UnitStatus status, DateTime expiresUtc, DateTime nowUtc)
    {
        if (NotLabelled.Contains(status))
        {
            return RuleResult.HardStop(Code, $"{unitNumber} is {status}: a component label is not printed for a unit that is not in stock to use.");
        }

        return expiresUtc <= nowUtc
            ? RuleResult.HardStop(Code, $"{unitNumber} expired {ClinicalTimeText.Local(expiresUtc)}: a component label is not printed for an expired unit.")
            : RuleResult.Pass(Code);
    }
}
