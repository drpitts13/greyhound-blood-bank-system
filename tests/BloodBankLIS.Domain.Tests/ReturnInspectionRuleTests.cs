using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

// TEST-BB-314: return inspection must be recorded before a unit can go back to stock (OCD-040);
// override authorizer is never filled for the user (OCD-041).
public class ReturnInspectionRuleTests
{
    [Fact]
    public void AllAnswered_Passes_WithTheAnswers()
    {
        var r = ReturnInspectionRule.Resolve(true, false, true, facilityDefaultAcceptable: false, mayReturnToAvailable: true);

        Assert.Equal(RuleSeverity.Pass, r.Result.Severity);
        Assert.True(r.TemperatureAcceptable);
        Assert.False(r.SealIntegrityAcceptable);
        Assert.True(r.VisualInspectionAcceptable);
        Assert.False(r.AnyUnanswered);
        Assert.False(r.UsedFacilityDefault);
    }

    [Fact]
    public void Unanswered_WhenTheUnitCouldGoBackToStock_IsAHardStop_NamingWhatIsMissing()
    {
        var r = ReturnInspectionRule.Resolve(null, true, null, facilityDefaultAcceptable: false, mayReturnToAvailable: true);

        Assert.Equal(RuleSeverity.HardStop, r.Result.Severity);
        Assert.Equal(ReturnInspectionRule.Code, r.Result.Code);
        Assert.Contains("temperature, visual inspection", r.Result.Message);
        Assert.DoesNotContain("seal", r.Result.Message);
    }

    [Fact]
    public void Unanswered_WithFacilityDefault_PassesAsAcceptable_AndIsFlagged()
    {
        var r = ReturnInspectionRule.Resolve(null, null, null, facilityDefaultAcceptable: true, mayReturnToAvailable: true);

        Assert.Equal(RuleSeverity.Pass, r.Result.Severity);
        Assert.True(r.TemperatureAcceptable && r.SealIntegrityAcceptable && r.VisualInspectionAcceptable);
        Assert.True(r.AnyUnanswered);
        Assert.True(r.UsedFacilityDefault);
    }

    [Fact]
    public void Unanswered_ForExplicitQuarantineOrDisposal_IsAllowed_AndNotCreditedToTheFacilityDefault()
    {
        var r = ReturnInspectionRule.Resolve(null, null, null, facilityDefaultAcceptable: false, mayReturnToAvailable: false);

        Assert.Equal(RuleSeverity.Pass, r.Result.Severity);
        Assert.True(r.AnyUnanswered);
        Assert.False(r.UsedFacilityDefault);
    }

    [Fact]
    public void Policy_ShipsOff_AsAnInventoryBoolean()
    {
        var def = FacilityPolicyCatalog.Find(FacilityPolicyKeys.InspectionDefaultAcceptable);

        Assert.NotNull(def);
        Assert.Equal("false", def.DefaultValue);
        Assert.Equal(FacilityPolicyValueKind.Boolean, def.Kind);
        Assert.False(FacilityPolicyCatalog.BoolDefault(FacilityPolicyKeys.InspectionDefaultAcceptable));
    }

    [Theory]
    [InlineData(null, null, null)]
    [InlineData("  ", "", null)]
    [InlineData("Massive hemorrhage", "Dr. Lee", null)]
    [InlineData("Massive hemorrhage", null, "name who authorized")]
    [InlineData(null, "Dr. Lee", "give the reason")]
    public void OverrideFields_SaysWhatIsMissing_OnlyOnceAnOverrideWasStarted(string? reason, string? authorizer, string? expected)
    {
        var r = OverrideFieldsRule.Missing(reason, authorizer);

        if (expected is null)
        {
            Assert.Null(r);
            return;
        }

        Assert.NotNull(r);
        Assert.Equal(RuleSeverity.Warning, r.Severity);
        Assert.Contains(expected, r.Message);
    }

    [Fact]
    public void OverrideFields_WithMissing_AppendsWithoutDroppingTheWarningsBeingOverridden()
    {
        var gate = new RuleEvaluation([RuleResult.Warning("ISS-XM-REQUIRED", "Crossmatch required.")]);

        var withMissing = OverrideFieldsRule.WithMissing(gate, "Hemorrhage", null);

        Assert.Equal(["ISS-XM-REQUIRED", OverrideFieldsRule.Code], withMissing.Results.Select(x => x.Code));
        Assert.Empty(withMissing.HardStops);
        Assert.Same(gate, OverrideFieldsRule.WithMissing(gate, null, null));
    }
}
