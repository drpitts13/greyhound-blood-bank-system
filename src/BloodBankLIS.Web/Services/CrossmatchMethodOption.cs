using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Web.Services;

/// <summary>
/// The crossmatch methods offered on Compatibility › Record crossmatch (end-user testing pass 174: Electronic was offered
/// while the facility had electronic crossmatch turned off, and choosing it only failed on save). When the facility
/// policy is known to be off, Electronic is shown but cannot be chosen, and says why.
/// </summary>
public static class CrossmatchMethodOption
{
    public static (string Text, bool Disabled) For(CrossmatchMethod method, string text, bool? facilityAllowsElectronic) =>
        method == CrossmatchMethod.Electronic && facilityAllowsElectronic == false
            ? ($"{text} (off at this facility)", true)
            : (text, false);
}
