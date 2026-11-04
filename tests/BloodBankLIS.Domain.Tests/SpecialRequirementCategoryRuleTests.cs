using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Domain.Rules.Config;
using Rule = BloodBankLIS.Domain.Rules.SpecialTransfusionRequirementRule;

namespace BloodBankLIS.Domain.Tests;

public class SpecialRequirementCategoryRuleTests
{
    private static readonly DateTime Now = new(2026, 11, 4, 12, 0, 0, DateTimeKind.Utc);
    private static readonly HashSet<string> NoAttributes = new(StringComparer.OrdinalIgnoreCase);

    private static Rule.RequirementRef Req(
        SpecialRequirementEnforcementKind kind,
        ComponentClass? componentClass,
        string? attribute = null,
        string? allowedAbo = null,
        string? unitTest = null,
        DateTime? start = null,
        DateTime? end = null,
        string name = "Requirement") =>
        new("CODE", kind, attribute, null, start ?? Now.AddDays(-1), end, true, componentClass, allowedAbo, unitTest, name);

    private static Rule.UnitTyping Unit(
        ComponentClass componentClass,
        AboGroup abo = AboGroup.A,
        RhType rh = RhType.Positive,
        params string[] negativeTests) =>
        new(componentClass, abo, rh, negativeTests.ToHashSet(StringComparer.Ordinal));

    private static IReadOnlyList<string> Unmet(Rule.RequirementRef req, Rule.UnitTyping unit, IReadOnlySet<string>? attributes = null) =>
        Rule.UnmetUnitRequirements([req], attributes ?? NoAttributes, [], Now, unit);

    [Fact]
    public void ARequirementForOneUnitCategory_IgnoresUnitsOfAnother_AndAllCategoriesAppliesToEvery()
    {
        var irradiatedRbc = Req(SpecialRequirementEnforcementKind.RequireProductAttribute, ComponentClass.RedBloodCells, "IRRAD");
        Assert.NotEmpty(Unmet(irradiatedRbc, Unit(ComponentClass.RedBloodCells)));
        Assert.Empty(Unmet(irradiatedRbc, Unit(ComponentClass.Platelets)));

        var allCategories = Req(SpecialRequirementEnforcementKind.RequireProductAttribute, null, "IRRAD");
        Assert.NotEmpty(Unmet(allCategories, Unit(ComponentClass.Platelets)));
        Assert.Empty(Unmet(allCategories, Unit(ComponentClass.Platelets), new HashSet<string>(["IRRAD"], StringComparer.OrdinalIgnoreCase)));
    }

    [Fact]
    public void UnitAbo_AcceptsOnlyTheAllowedGroups_AndRhNegative_OnlyRhNegativeUnits()
    {
        var groupO = Req(SpecialRequirementEnforcementKind.RequireUnitAbo, ComponentClass.RedBloodCells, allowedAbo: "O", name: "Group O red cells only");
        Assert.Empty(Unmet(groupO, Unit(ComponentClass.RedBloodCells, AboGroup.O)));
        var refused = Assert.Single(Unmet(groupO, Unit(ComponentClass.RedBloodCells, AboGroup.A)));
        Assert.Contains("Group O red cells only", refused);
        Assert.Contains("must be group O", refused);
        // The group O rule is for red cells; group A plasma is unaffected.
        Assert.Empty(Unmet(groupO, Unit(ComponentClass.Plasma, AboGroup.A)));

        var oOrA = Req(SpecialRequirementEnforcementKind.RequireUnitAbo, ComponentClass.RedBloodCells, allowedAbo: "O,A");
        Assert.Empty(Unmet(oOrA, Unit(ComponentClass.RedBloodCells, AboGroup.A)));
        Assert.NotEmpty(Unmet(oOrA, Unit(ComponentClass.RedBloodCells, AboGroup.B)));

        var rhNeg = Req(SpecialRequirementEnforcementKind.RequireRhNegative, ComponentClass.Platelets);
        Assert.Empty(Unmet(rhNeg, Unit(ComponentClass.Platelets, rh: RhType.Negative)));
        Assert.NotEmpty(Unmet(rhNeg, Unit(ComponentClass.Platelets, rh: RhType.Positive)));
    }

    [Fact]
    public void UnitTestNegative_IsMetByTheProductCode_OrANegativeTestOnTheUnit_AndNotOtherwise()
    {
        var cmv = Req(SpecialRequirementEnforcementKind.RequireUnitTestNegative, ComponentClass.RedBloodCells, "CMVNEG", unitTest: "CMV");
        var byProduct = new HashSet<string>(["CMVNEG"], StringComparer.OrdinalIgnoreCase);
        Assert.Empty(Unmet(cmv, Unit(ComponentClass.RedBloodCells), byProduct));
        Assert.Empty(Unmet(cmv, Unit(ComponentClass.RedBloodCells, negativeTests: "CMV")));
        Assert.NotEmpty(Unmet(cmv, Unit(ComponentClass.RedBloodCells)));

        var hbs = Req(SpecialRequirementEnforcementKind.RequireUnitTestNegative, ComponentClass.RedBloodCells, unitTest: "HBS");
        Assert.Empty(Unmet(hbs, Unit(ComponentClass.RedBloodCells, negativeTests: "HBS")));
        Assert.NotEmpty(Unmet(hbs, Unit(ComponentClass.RedBloodCells, negativeTests: "CMV")));

        Assert.True(Rule.IsNegativeResult(" Negative "));
        Assert.True(Rule.IsNegativeResult("neg"));
        Assert.False(Rule.IsNegativeResult("Positive"));
        Assert.False(Rule.IsNegativeResult(null));
    }

    [Fact]
    public void Informational_NeverBlocks_SelectionOrIssue()
    {
        var info = Req(SpecialRequirementEnforcementKind.Informational, null);
        Assert.Empty(Unmet(info, Unit(ComponentClass.RedBloodCells)));
        Assert.False(Rule.FailsUnitSelection([info], NoAttributes, [], Now, Unit(ComponentClass.RedBloodCells)));
        Assert.True(Rule.ProceduresMet([info], Now, null, AboGroup.A, AboSubgroup.Unknown));
    }

    [Fact]
    public void ARequirement_AppliesFromItsStart_AndNoLongerOnOrAfterItsEnd()
    {
        var start = Now.AddHours(-1);
        var end = Now.AddHours(1);
        var windowed = Req(SpecialRequirementEnforcementKind.RequireUnitAbo, ComponentClass.RedBloodCells, allowedAbo: "O", start: start, end: end);
        var unitA = Unit(ComponentClass.RedBloodCells, AboGroup.A);

        Assert.NotEmpty(Rule.UnmetUnitRequirements([windowed], NoAttributes, [], start, unitA));
        Assert.NotEmpty(Rule.UnmetUnitRequirements([windowed], NoAttributes, [], end.AddTicks(-1), unitA));
        Assert.Empty(Rule.UnmetUnitRequirements([windowed], NoAttributes, [], end, unitA));
        Assert.Empty(Rule.UnmetUnitRequirements([windowed], NoAttributes, [], start.AddTicks(-1), unitA));
    }

    [Fact]
    public void AtIssue_AnUnmetTypingStops_ButOnEmergencyRelease_NeedsTheOverride_WhileProceduresStillStop()
    {
        IssueGateContext Context(bool emergency, bool proceduresMet) => new()
        {
            IdentityConfirmed = true,
            SpecimenExists = true,
            SpecimenBelongsToPatient = true,
            PatientBloodTypeKnown = true,
            UnitAboRh = new ValueObjects.AboRh(AboGroup.A, RhType.Positive),
            ComponentClass = ComponentClass.RedBloodCells,
            UnitStatus = UnitStatus.Allocated,
            UnitExpiresUtc = Now.AddDays(10),
            NowUtc = Now,
            AllocatedToThisPatient = true,
            RequiresCrossmatch = false,
            HasValidCrossmatch = true,
            IsEmergencyRelease = emergency,
            ProductTypeMatchesOrder = true,
            SpecialRequirementsMet = proceduresMet,
            UnmetUnitRequirements = ["Patient requires Group O red cells only: the unit must be group O, and it is A."],
            UnresolvedAboRhDiscrepancy = false
        };

        var routine = IssueGate.EvaluateSpecialRequirements(Context(emergency: false, proceduresMet: true));
        Assert.Contains(routine, r => r.Code == IssueGate.SpecialReqCode && r.Severity == RuleSeverity.HardStop && r.Message.Contains("Group O"));

        var emergency = IssueGate.EvaluateSpecialRequirements(Context(emergency: true, proceduresMet: true));
        var warning = Assert.Single(emergency);
        Assert.Equal(RuleSeverity.Warning, warning.Severity);
        Assert.Contains("authorized override", warning.Message);

        var procedureMissed = IssueGate.EvaluateSpecialRequirements(Context(emergency: true, proceduresMet: false));
        Assert.Contains(procedureMissed, r => r.Severity == RuleSeverity.HardStop);
    }

    [Fact]
    public void Validator_PutsEachKindInItsTable_AndAsksForWhatTheKindNeeds()
    {
        static SpecialRequirementDefinition Def(SpecialRequirementCategory category, SpecialRequirementEnforcementKind kind) =>
            new() { Code = "X", Name = "X", Category = category, EnforcementKind = kind };

        var wrongTable = SpecialRequirementDefinitionValidator.Validate(
            Def(SpecialRequirementCategory.UnitModification, SpecialRequirementEnforcementKind.RequireUnitAbo), false, true);
        Assert.Contains(wrongTable.HardStops, r => r.Code == "SRDEF.KIND.CATEGORY");

        var noClass = SpecialRequirementDefinitionValidator.Validate(
            Def(SpecialRequirementCategory.UnitModification, SpecialRequirementEnforcementKind.RequireProductAttribute),
            false, true, requireComponentClass: true);
        Assert.Contains(noClass.HardStops, r => r.Code == "SRDEF.CLASS.REQUIRED");

        var abo = Def(SpecialRequirementCategory.UnitTyping, SpecialRequirementEnforcementKind.RequireUnitAbo);
        abo.ComponentClass = ComponentClass.RedBloodCells;
        Assert.Contains(SpecialRequirementDefinitionValidator.Validate(abo, false, true, true).HardStops, r => r.Code == "SRDEF.ABO.REQUIRED");
        abo.AllowedAboGroups = "O";
        Assert.False(SpecialRequirementDefinitionValidator.Validate(abo, false, true, true).IsHardStopped);

        var test = Def(SpecialRequirementCategory.UnitTyping, SpecialRequirementEnforcementKind.RequireUnitTestNegative);
        test.ComponentClass = ComponentClass.RedBloodCells;
        Assert.Contains(SpecialRequirementDefinitionValidator.Validate(test, false, true, true).HardStops, r => r.Code == "SRDEF.TEST.REQUIRED");

        var info = Def(SpecialRequirementCategory.Informational, SpecialRequirementEnforcementKind.Informational);
        Assert.Contains(SpecialRequirementDefinitionValidator.Validate(info, false, true).HardStops, r => r.Code == "SRDEF.BANNER.REQUIRED");
        info.ComponentClass = ComponentClass.Plasma;
        info.Instruction = "Call the physician.";
        Assert.Contains(SpecialRequirementDefinitionValidator.Validate(info, false, true).HardStops, r => r.Code == "SRDEF.CLASS.NOT.APPLICABLE");
    }

    [Fact]
    public void ChoiceLabels_NameTheUnitCategory()
    {
        Assert.Equal("Irradiated – RBC",
            SpecialRequirementLabel.Choice("Irradiated", SpecialRequirementCategory.UnitModification, ComponentClass.RedBloodCells));
        Assert.Equal("Irradiated", SpecialRequirementLabel.Choice("Irradiated", SpecialRequirementCategory.UnitModification, null));
        Assert.Equal("All categories", SpecialRequirementLabel.UnitCategory(SpecialRequirementCategory.UnitTyping, null));
        Assert.Equal(string.Empty, SpecialRequirementLabel.UnitCategory(SpecialRequirementCategory.Informational, null));
    }
}
