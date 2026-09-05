using BloodBankLIS.Domain.Entities;
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

    [Fact]
    public void CellularIssue_WithoutSecondType_IsHardStop()
    {
        var result = SecondAboDeterminationRule.EvaluateForCellularIssue(
            required: true,
            hasSecondConcordant: false,
            ComponentClass.RedBloodCells,
            isEmergencyRelease: false);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(SecondAboDeterminationRule.IssueCode, result.Code);
    }

    [Fact]
    public void EmergencyCellularIssue_WithoutSecondType_IsWarning()
    {
        var result = SecondAboDeterminationRule.EvaluateForCellularIssue(
            required: true,
            hasSecondConcordant: false,
            ComponentClass.RedBloodCells,
            isEmergencyRelease: true);
        Assert.Equal(RuleSeverity.Warning, result.Severity);
    }

    [Fact]
    public void PlasmaIssue_SkipsSecondType()
    {
        var result = SecondAboDeterminationRule.EvaluateForCellularIssue(
            required: true,
            hasSecondConcordant: false,
            ComponentClass.Plasma,
            isEmergencyRelease: false);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }
}

public class OrderControlRuleTests
{
    [Fact]
    public void HoldThenRelease_ReturnsToInProcess()
    {
        var order = new Order { Status = OrderStatus.New };
        Assert.Equal(RuleSeverity.Pass, OrderControlRule.Apply(order, "HD", null).Severity);
        Assert.Equal(OrderStatus.OnHold, order.Status);
        Assert.Equal(RuleSeverity.Pass, OrderControlRule.Apply(order, "RL", null).Severity);
        Assert.Equal(OrderStatus.InProcess, order.Status);
    }

    [Fact]
    public void CancelCompleted_IsHardStop()
    {
        var order = new Order { Status = OrderStatus.Completed };
        var result = OrderControlRule.Apply(order, "CA", null);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(OrderStatus.Completed, order.Status);
    }

    [Fact]
    public void IssueAgainstHeldOrder_IsHardStop()
    {
        var result = OrderControlRule.EvaluateIssue(orderLinked: true, orderIsFulfillable: false);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Equal(OrderControlRule.IssueCode, result.Code);
    }
}

public class EmergencyUncrossmatchedAboRuleTests
{
    [Fact]
    public void NonO_UncrossmatchedRbc_IsWarning()
    {
        var results = EmergencyUncrossmatchedAboRule.Evaluate(
            true,
            ComponentClass.RedBloodCells,
            new AboRh(AboGroup.A, RhType.Positive),
            new AboRh(AboGroup.O, RhType.Positive),
            Sex.Male,
            40,
            requireGroupO: true,
            requireONegForChildbearing: true,
            childbearingAgeYears: 50);
        Assert.Contains(results, r => r.Code == EmergencyUncrossmatchedAboRule.AboCode && r.Severity == RuleSeverity.Warning);
    }

    [Fact]
    public void KnownRhPositive_SkipsChildbearingRhWarning()
    {
        var results = EmergencyUncrossmatchedAboRule.Evaluate(
            true,
            ComponentClass.RedBloodCells,
            new AboRh(AboGroup.O, RhType.Positive),
            new AboRh(AboGroup.O, RhType.Positive),
            Sex.Female,
            25,
            requireGroupO: true,
            requireONegForChildbearing: true,
            childbearingAgeYears: 50);
        Assert.DoesNotContain(results, r => r.Severity != RuleSeverity.Pass);
    }
}
