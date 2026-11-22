using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

// TEST-BB-673: a hard stop for a special requirement names it as every screen does: a row with no catalog entry by its
// type's name ("Irradiated", "Antigen-negative"), a catalog entry by its name; an acknowledgment due at issue names the
// requirement, not its code. A configuration error keeps the code beside the name for the administrator.
public class SpecialRequirementMessageNamesTests
{
    private static readonly DateTime Now = new(2026, 11, 8, 12, 0, 0, DateTimeKind.Utc);

    private static IReadOnlyList<RuleResult> Check(SpecialTransfusionRequirementRule.RequirementRef requirement) =>
        SpecialTransfusionRequirementRule.Evaluate([requirement], new HashSet<string>(), [], Now);

    [Fact]
    public void HardStops_NameTheRequirement_NotItsCode()
    {
        var irradiated = SpecialTransfusionRequirementRule.RequirementRef.FromLegacy(
            SpecialTransfusionRequirementType.Irradiated, null, Now.AddDays(-1), null, true);
        var stop = Assert.Single(Check(irradiated), r => r.Severity == RuleSeverity.HardStop);
        Assert.StartsWith("Patient requires Irradiated:", stop.Message);
        Assert.DoesNotContain(SpecialRequirementCatalog.Irradiated + ":", stop.Message);

        var antigen = SpecialTransfusionRequirementRule.RequirementRef.FromLegacy(
            SpecialTransfusionRequirementType.AntigenNegative, "K", Now.AddDays(-1), null, true);
        Assert.Contains(Check(antigen), r => r.Severity == RuleSeverity.HardStop && r.Message.StartsWith("Patient requires Antigen-negative:"));

        var call = new SpecialTransfusionRequirementRule.RequirementRef(
            "CALL-TSMD", SpecialRequirementEnforcementKind.RequireIssueAcknowledgment, null, null, Now.AddDays(-1), null, true,
            Name: "Call the transfusion service physician");
        Assert.Contains(Check(call), r => r.Message == "Active special requirement Call the transfusion service physician must be acknowledged at issue.");
        Assert.DoesNotContain(SpecialTransfusionRequirementRule.Evaluate([call], new HashSet<string>(), [], Now,
            acknowledgedCodes: new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "CALL-TSMD" }), r => r.Severity == RuleSeverity.HardStop);

        var misconfigured = new SpecialTransfusionRequirementRule.RequirementRef(
            "IRRAD-PLT", SpecialRequirementEnforcementKind.RequireProductAttribute, null, null, Now.AddDays(-1), null, true,
            Name: "Irradiated platelets");
        Assert.Contains(Check(misconfigured), r => r.Message == "Special requirement Irradiated platelets (IRRAD-PLT) is missing a product attribute code.");
    }
}
