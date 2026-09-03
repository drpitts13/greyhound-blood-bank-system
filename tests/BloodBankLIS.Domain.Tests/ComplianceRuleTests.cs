using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Domain.ValueObjects;

namespace BloodBankLIS.Domain.Tests;

public class SpecialTransfusionRequirementRuleTests
{
    private static readonly DateTime Now = new(2026, 8, 18, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void NoActiveRequirements_Passes()
    {
        var results = SpecialTransfusionRequirementRule.Evaluate([], new HashSet<string>(), [], Now);
        Assert.True(SpecialTransfusionRequirementRule.AllMet(results));
    }

    [Fact]
    public void IrradiatedMissing_IsHardStop()
    {
        var req = new SpecialTransfusionRequirementRule.RequirementRef(
            SpecialTransfusionRequirementType.Irradiated, null, Now.AddDays(-1), null, true);
        var results = SpecialTransfusionRequirementRule.Evaluate([req], new HashSet<string> { "LR" }, [], Now);
        Assert.Contains(results, r => r.Severity == RuleSeverity.HardStop && r.Code == IssueGate.SpecialReqCode);
    }

    [Fact]
    public void IrradiatedPresent_Passes()
    {
        var req = new SpecialTransfusionRequirementRule.RequirementRef(
            SpecialTransfusionRequirementType.Irradiated, null, Now.AddDays(-1), null, true);
        var results = SpecialTransfusionRequirementRule.Evaluate([req], new HashSet<string> { "IRRAD" }, [], Now);
        Assert.True(SpecialTransfusionRequirementRule.AllMet(results));
    }

    [Fact]
    public void AntigenNegativeUnmet_IsHardStop()
    {
        var req = new SpecialTransfusionRequirementRule.RequirementRef(
            SpecialTransfusionRequirementType.AntigenNegative, "K", Now.AddDays(-1), null, true);
        var results = SpecialTransfusionRequirementRule.Evaluate(
            [req],
            new HashSet<string>(),
            [new BloodAttributeCompatibilityRule.AntigenRef("K", AntigenResult.Positive)],
            Now);
        Assert.Contains(results, r => r.Severity == RuleSeverity.HardStop);
    }
}

public class PatientIdentityMatchRuleTests
{
    [Fact]
    public void MatchingMrnAndDob_Passes()
    {
        var result = PatientIdentityMatchRule.Evaluate(
            "MRN1", new DateOnly(1975, 6, 1), "Issue", "Test",
            new PatientIdentityMatchRule.IdentityToken(IdentityTokenType.MedicalRecordNumber, "MRN1"),
            new PatientIdentityMatchRule.IdentityToken(IdentityTokenType.DateOfBirth, "1975-06-01"));
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }

    [Fact]
    public void SameType_IsHardStop()
    {
        var result = PatientIdentityMatchRule.Evaluate(
            "MRN1", new DateOnly(1975, 6, 1), "Issue", "Test",
            new PatientIdentityMatchRule.IdentityToken(IdentityTokenType.MedicalRecordNumber, "MRN1"),
            new PatientIdentityMatchRule.IdentityToken(IdentityTokenType.MedicalRecordNumber, "MRN1"));
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
    }

    [Fact]
    public void MismatchedValue_IsHardStop()
    {
        var result = PatientIdentityMatchRule.Evaluate(
            "MRN1", new DateOnly(1975, 6, 1), "Issue", "Test",
            new PatientIdentityMatchRule.IdentityToken(IdentityTokenType.MedicalRecordNumber, "OTHER"),
            new PatientIdentityMatchRule.IdentityToken(IdentityTokenType.DateOfBirth, "1975-06-01"));
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
    }
}

public class SpecimenValidityPolicyTests
{
    [Fact]
    public void AlloimmunizationRisk_UsesThreeDays()
    {
        var collected = new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc);
        var expires = SpecimenValidityPolicy.ComputeExpiresUtc(collected, alloimmunizationRisk: true);
        Assert.Equal(collected.AddHours(72), expires);
    }

    [Fact]
    public void NoRisk_UsesStandardWindow()
    {
        var collected = new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc);
        var expires = SpecimenValidityPolicy.ComputeExpiresUtc(collected, alloimmunizationRisk: false);
        Assert.Equal(collected.AddHours(168), expires);
    }

    [Fact]
    public void RecentTransfusion_IsRisk()
    {
        var now = new DateTime(2026, 8, 18, 0, 0, 0, DateTimeKind.Utc);
        Assert.True(SpecimenValidityPolicy.HasAlloimmunizationRisk(now, now.AddDays(-10), null));
        Assert.False(SpecimenValidityPolicy.HasAlloimmunizationRisk(now, now.AddDays(-120), null));
    }
}

public class DualIdentificationRuleTests
{
    [Fact]
    public void DistinctSecondVerifier_Passes()
    {
        var result = DualIdentificationRule.Evaluate("tech1", "tech2", false, required: true);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }

    [Fact]
    public void SameUser_IsHardStop()
    {
        var result = DualIdentificationRule.Evaluate("tech1", "tech1", false, required: true);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
    }

    [Fact]
    public void ElectronicIdentification_PassesWithoutSecondPerson()
    {
        var result = DualIdentificationRule.Evaluate("tech1", null, electronicIdentificationComplete: true, required: true);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }
}

public class SecondVerifierDirectoryRuleTests
{
    [Fact]
    public void Empty_Passes()
    {
        var result = SecondVerifierDirectoryRule.Evaluate(null, isActiveUser: false);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }

    [Fact]
    public void ActiveUser_Passes()
    {
        var result = SecondVerifierDirectoryRule.Evaluate("tech2", isActiveUser: true);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }

    [Fact]
    public void UnknownOrInactive_IsHardStop()
    {
        var result = SecondVerifierDirectoryRule.Evaluate("initials-only", isActiveUser: false);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(SecondVerifierDirectoryRule.Code, result.Code);
    }
}

public class ReturnReissueRuleTests
{
    [Fact]
    public void AllConditionsMet_Passes()
    {
        var result = ReturnReissueRule.Evaluate(true, true, true, true, true);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }

    [Fact]
    public void FailedVisual_IsHardStop()
    {
        var result = ReturnReissueRule.Evaluate(true, true, false, true, true);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
    }

    [Fact]
    public void TemperatureFail_IsWarningQuarantine()
    {
        var result = ReturnReissueRule.Evaluate(false, true, true, true, true);
        Assert.Equal(RuleSeverity.Warning, result.Severity);
    }
}

public class SelfVerifyRuleTests
{
    [Fact]
    public void SameUserWhenBlocked_IsHardStop()
    {
        var result = SelfVerifyRule.Evaluate("tech1", "tech1", blockSelfVerify: true);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
    }

    [Fact]
    public void SameUserWhenAllowed_Passes()
    {
        var result = SelfVerifyRule.Evaluate("tech1", "tech1", blockSelfVerify: false);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }
}

public class SecondAboDeterminationRuleTests
{
    [Fact]
    public void CurrentPlusMatchingHistory_IsConcordant()
    {
        var current = new AboRh(AboGroup.A, RhType.Positive);
        var ok = SecondAboDeterminationRule.HasSecondConcordant(
        [
            new(current, true),
            new(current, false)
        ]);
        Assert.True(ok);
    }

    [Fact]
    public void OnlyCurrent_IsNotConcordant()
    {
        var current = new AboRh(AboGroup.A, RhType.Positive);
        Assert.False(SecondAboDeterminationRule.HasSecondConcordant([new(current, true)]));
    }
}
