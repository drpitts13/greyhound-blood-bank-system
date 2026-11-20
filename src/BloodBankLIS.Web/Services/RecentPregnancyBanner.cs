using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Web.Services;

/// <summary>
/// The chart header's note of a recent pregnancy (end-user testing pass 309: Gravid, Elena's pregnancy date was shown only
/// near the foot of the Overview tab, and only to those who may edit it, so the bench collecting her specimen did not see
/// that her specimens take the shorter alloimmunization-risk window).
/// </summary>
public static class RecentPregnancyBanner
{
    public static string? For(DateTime? recentPregnancyUtc, DateTime nowUtc) =>
        recentPregnancyUtc is DateTime pregnancy && SpecimenValidityPolicy.HasAlloimmunizationRisk(nowUtc, null, pregnancy)
            ? $"Recent pregnancy {ClinicalTimeText.Local(pregnancy)[..10]}: specimens take the shorter alloimmunization-risk window."
            : null;
}
