using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules.Config;
using BloodBankLIS.Domain.ValueObjects;

namespace BloodBankLIS.Domain.Tests;

public class ConfigValidatorsTests
{
    [Fact]
    public void TestDefinition_Valid_Passes()
    {
        var def = new TestDefinition
        {
            Code = "ABORH",
            Name = "ABO/Rh",
            ResultValueType = ResultValueType.Coded,
            AllowedResultValues = "A,B,O,AB"
        };

        var eval = TestDefinitionValidator.Validate(def, duplicateActiveCode: false);

        Assert.False(eval.IsHardStopped);
        Assert.True(eval.IsAllowed);
    }

    [Fact]
    public void TestDefinition_MissingCodeAndName_HardStops()
    {
        var def = new TestDefinition { Code = "", Name = "" };

        var eval = TestDefinitionValidator.Validate(def, duplicateActiveCode: false);

        Assert.True(eval.IsHardStopped);
        Assert.Contains(eval.HardStops, r => r.Code == "TESTDEF.CODE.REQUIRED");
        Assert.Contains(eval.HardStops, r => r.Code == "TESTDEF.NAME.REQUIRED");
    }

    [Fact]
    public void TestDefinition_DuplicateActiveCode_HardStops()
    {
        var def = new TestDefinition { Code = "ABORH", Name = "ABO/Rh", ResultValueType = ResultValueType.FreeText };

        var eval = TestDefinitionValidator.Validate(def, duplicateActiveCode: true);

        Assert.True(eval.IsHardStopped);
        Assert.Contains(eval.HardStops, r => r.Code == "TESTDEF.CODE.DUPLICATE");
    }

    [Fact]
    public void TestDefinition_CodedWithoutAllowedValues_Warns()
    {
        var def = new TestDefinition { Code = "X", Name = "X", ResultValueType = ResultValueType.Coded };

        var eval = TestDefinitionValidator.Validate(def, duplicateActiveCode: false);

        Assert.False(eval.IsHardStopped);
        Assert.Contains(eval.Warnings, r => r.Code == "TESTDEF.ALLOWED.MISSING");
    }

    [Fact]
    public void Product_InvalidShelfLife_HardStops()
    {
        var product = new ProductType
        {
            ProductCode = "RBC",
            Name = "Red Cells",
            DefaultShelfLifeHours = 0
        };

        var eval = ProductDefinitionValidator.Validate(product, duplicateActiveCode: false);

        Assert.True(eval.IsHardStopped);
        Assert.Contains(eval.HardStops, r => r.Code == "PRODUCT.SHELFLIFE.INVALID");
    }

    [Fact]
    public void Product_CrossmatchWithoutAbo_Warns()
    {
        var product = new ProductType
        {
            ProductCode = "RBC",
            Name = "Red Cells",
            RequiresCrossmatch = true,
            RequiresAboMatch = false
        };

        var eval = ProductDefinitionValidator.Validate(product, duplicateActiveCode: false);

        Assert.False(eval.IsHardStopped);
        Assert.Contains(eval.Warnings, r => r.Code == "PRODUCT.ABO.UNSAFE");
    }

    [Fact]
    public void Hl7Endpoint_MllpWithoutHostOrPort_HardStops()
    {
        var endpoint = new InterfaceEndpoint
        {
            Name = "Inbound",
            MessageTypes = "ORU",
            Transport = InterfaceTransport.Mllp,
            Host = null,
            Port = null
        };

        var eval = Hl7EndpointValidator.Validate(endpoint, duplicateActiveName: false, duplicateActiveHostPort: false);

        Assert.True(eval.IsHardStopped);
        Assert.Contains(eval.HardStops, r => r.Code == "HL7EP.HOST.REQUIRED");
        Assert.Contains(eval.HardStops, r => r.Code == "HL7EP.PORT.INVALID");
    }

    [Fact]
    public void Hl7Endpoint_Valid_Passes()
    {
        var endpoint = new InterfaceEndpoint
        {
            Name = "Inbound",
            MessageTypes = "ORU",
            Transport = InterfaceTransport.Mllp,
            Host = "10.0.0.1",
            Port = 2575
        };

        var eval = Hl7EndpointValidator.Validate(endpoint, duplicateActiveName: false, duplicateActiveHostPort: false);

        Assert.False(eval.IsHardStopped);
    }

    [Fact]
    public void Hl7Endpoint_DuplicateName_HardStops()
    {
        var endpoint = new InterfaceEndpoint
        {
            Name = "Inbound",
            MessageTypes = "ORU",
            Transport = InterfaceTransport.File
        };

        var eval = Hl7EndpointValidator.Validate(endpoint, duplicateActiveName: true, duplicateActiveHostPort: false);

        Assert.True(eval.IsHardStopped);
        Assert.Contains(eval.HardStops, r => r.Code == "HL7EP.NAME.DUPLICATE");
    }

    [Fact]
    public void Hl7Endpoint_RequiredMappingBlank_HardStops()
    {
        var endpoint = new InterfaceEndpoint
        {
            Name = "Epic ADT",
            InterfaceType = InterfaceType.Adt,
            Direction = Hl7Direction.Inbound,
            MessageTypes = "ADT",
            Transport = InterfaceTransport.File
        };

        var mappings = new List<InterfaceFieldMapping>
        {
            new() { DataItemKey = "Patient.MedicalRecordNumber", Hl7Path = "", IsRequired = true }
        };

        var eval = Hl7EndpointValidator.Validate(endpoint, duplicateActiveName: false, duplicateActiveHostPort: false, mappings);

        Assert.True(eval.IsHardStopped);
        Assert.Contains(eval.HardStops, r => r.Code == "HL7EP.MAP.REQUIRED");
    }

    [Fact]
    public void Hl7Endpoint_InvalidHl7Path_HardStops()
    {
        var endpoint = new InterfaceEndpoint
        {
            Name = "Epic ADT",
            InterfaceType = InterfaceType.Adt,
            Direction = Hl7Direction.Inbound,
            MessageTypes = "ADT",
            Transport = InterfaceTransport.File
        };

        var mappings = new List<InterfaceFieldMapping>
        {
            new() { DataItemKey = "Patient.MedicalRecordNumber", Hl7Path = "PID3", IsRequired = true }
        };

        var eval = Hl7EndpointValidator.Validate(endpoint, duplicateActiveName: false, duplicateActiveHostPort: false, mappings);

        Assert.True(eval.IsHardStopped);
        Assert.Contains(eval.HardStops, r => r.Code == "HL7EP.MAP.PATH");
    }

    [Fact]
    public void Hl7Endpoint_UnusualDirection_Warns()
    {
        var endpoint = new InterfaceEndpoint
        {
            Name = "ADT out",
            InterfaceType = InterfaceType.Adt,
            Direction = Hl7Direction.Outbound,
            MessageTypes = "ADT",
            Transport = InterfaceTransport.File
        };

        var eval = Hl7EndpointValidator.Validate(endpoint, duplicateActiveName: false, duplicateActiveHostPort: false);

        Assert.False(eval.IsHardStopped);
        Assert.Contains(eval.Warnings, r => r.Code == "HL7EP.DIRECTION.UNUSUAL");
    }

    [Fact]
    public void TestDefinition_BloodAttributeWithoutScope_HardStops()
    {
        var def = new TestDefinition
        {
            Code = "AGTYPE",
            Name = "Antigen Typing",
            ResultValueType = ResultValueType.BloodAttribute,
            BloodAttributeScopeKind = BloodAttributeKind.Antigen
        };

        var eval = TestDefinitionValidator.Validate(def, duplicateActiveCode: false);

        Assert.True(eval.IsHardStopped);
        Assert.Contains(eval.HardStops, r => r.Code == "TESTDEF.BLOODATTR.SCOPE.MISSING");
    }

    [Fact]
    public void TestDefinition_BloodAttributeWithoutKind_HardStops()
    {
        var def = new TestDefinition
        {
            Code = "ABID",
            Name = "Antibody ID",
            ResultValueType = ResultValueType.BloodAttribute,
            BloodAttributeScopeJson = """[{"code":"E"}]"""
        };

        var eval = TestDefinitionValidator.Validate(def, duplicateActiveCode: false);

        Assert.True(eval.IsHardStopped);
        Assert.Contains(eval.HardStops, r => r.Code == "TESTDEF.BLOODATTR.KIND.MISSING");
    }

    [Fact]
    public void TestDefinition_BloodAttributeUnknownCatalogCode_HardStops()
    {
        var def = new TestDefinition
        {
            Code = "AGTYPE",
            Name = "Antigen Typing",
            ResultValueType = ResultValueType.BloodAttribute,
            BloodAttributeScopeKind = BloodAttributeKind.Antigen,
            BloodAttributeScopeJson = """[{"code":"K"},{"code":"UNKNOWN"}]"""
        };

        var activeCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "K" };
        var eval = TestDefinitionValidator.Validate(def, duplicateActiveCode: false, activeBloodAttributeCodes: activeCodes);

        Assert.True(eval.IsHardStopped);
        Assert.Contains(eval.HardStops, r => r.Code == "TESTDEF.BLOODATTR.SCOPE.MISSING" && r.Message.Contains("UNKNOWN"));
    }

    [Fact]
    public void TestDefinition_BloodAttributeValidScope_Passes()
    {
        var def = new TestDefinition
        {
            Code = "AGTYPE",
            Name = "Antigen Typing",
            ResultValueType = ResultValueType.BloodAttribute,
            BloodAttributeScopeKind = BloodAttributeKind.Antigen,
            BloodAttributeScopeJson = """[{"code":"K"},{"code":"FYA"}]"""
        };

        var activeCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "K", "FYA" };
        var eval = TestDefinitionValidator.Validate(def, duplicateActiveCode: false, activeBloodAttributeCodes: activeCodes);

        Assert.False(eval.IsHardStopped);
    }

    [Fact]
    public void ReflexRule_Valid_Passes()
    {
        var rule = new ReflexRule
        {
            Code = "ABSC-POS-ABID",
            Name = "Positive screen to ID",
            TriggerTestCode = "ABSC",
            TriggerResultValue = "Positive",
            ReflexTestCode = "ABID"
        };
        var active = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "ABSC", "ABID" };

        var eval = ReflexRuleValidator.Validate(rule, duplicateActiveCode: false, duplicateActiveTriple: false, active);

        Assert.False(eval.IsHardStopped);
    }

    [Fact]
    public void ReflexRule_MissingFields_HardStops()
    {
        var rule = new ReflexRule();

        var eval = ReflexRuleValidator.Validate(rule, duplicateActiveCode: false, duplicateActiveTriple: false);

        Assert.True(eval.IsHardStopped);
        Assert.Contains(eval.HardStops, r => r.Code == "REFLEX.CODE.REQUIRED");
        Assert.Contains(eval.HardStops, r => r.Code == "REFLEX.NAME.REQUIRED");
        Assert.Contains(eval.HardStops, r => r.Code == "REFLEX.TRIGGER.REQUIRED");
        Assert.Contains(eval.HardStops, r => r.Code == "REFLEX.VALUE.REQUIRED");
        Assert.Contains(eval.HardStops, r => r.Code == "REFLEX.REFLEX.REQUIRED");
    }

    [Fact]
    public void ReflexRule_SelfReflex_HardStops()
    {
        var rule = new ReflexRule
        {
            Code = "SELF",
            Name = "Self",
            TriggerTestCode = "ABSC",
            TriggerResultValue = "Positive",
            ReflexTestCode = "absc"
        };

        var eval = ReflexRuleValidator.Validate(rule, duplicateActiveCode: false, duplicateActiveTriple: false);

        Assert.True(eval.IsHardStopped);
        Assert.Contains(eval.HardStops, r => r.Code == "REFLEX.SELF");
    }

    [Fact]
    public void ReflexRule_UnknownTests_HardStops()
    {
        var rule = new ReflexRule
        {
            Code = "X",
            Name = "X",
            TriggerTestCode = "ABSC",
            TriggerResultValue = "Positive",
            ReflexTestCode = "ABID"
        };
        var active = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "ABSC" };

        var eval = ReflexRuleValidator.Validate(rule, duplicateActiveCode: false, duplicateActiveTriple: false, active);

        Assert.True(eval.IsHardStopped);
        Assert.Contains(eval.HardStops, r => r.Code == "REFLEX.REFLEX.MISSING");
    }

    [Fact]
    public void ReflexRule_DuplicateCodeOrTriple_HardStops()
    {
        var rule = new ReflexRule
        {
            Code = "ABSC-POS-ABID",
            Name = "Positive screen to ID",
            TriggerTestCode = "ABSC",
            TriggerResultValue = "Positive",
            ReflexTestCode = "ABID"
        };
        var active = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "ABSC", "ABID" };

        var codeDup = ReflexRuleValidator.Validate(rule, duplicateActiveCode: true, duplicateActiveTriple: false, active);
        Assert.Contains(codeDup.HardStops, r => r.Code == "REFLEX.CODE.DUPLICATE");

        var tripleDup = ReflexRuleValidator.Validate(rule, duplicateActiveCode: false, duplicateActiveTriple: true, active);
        Assert.Contains(tripleDup.HardStops, r => r.Code == "REFLEX.TRIPLE.DUPLICATE");
    }

    [Fact]
    public void ModificationRule_Valid_Passes()
    {
        var rule = ValidModificationRule();

        var eval = ModificationRuleValidator.Validate(rule, duplicateActiveTriple: false, sourceProductActive: true, targetProductActive: true, expirationCodeActive: true);

        Assert.False(eval.IsHardStopped);
    }

    [Fact]
    public void ModificationRule_MissingSourceAndTarget_HardStops()
    {
        var rule = new ModificationRule
        {
            ModificationCode = "DIV-TEST",
            SourceProductTypeId = 0,
            TargetProductTypeId = 0,
            ModificationType = ModificationType.Divide,
            ExpirationModificationCodeId = 1
        };

        var eval = ModificationRuleValidator.Validate(rule, duplicateActiveTriple: false);

        Assert.True(eval.IsHardStopped);
        Assert.Contains(eval.HardStops, r => r.Code == "MODRULE.SOURCE.REQUIRED");
        Assert.Contains(eval.HardStops, r => r.Code == "MODRULE.TARGET.REQUIRED");
    }

    [Fact]
    public void ModificationRule_MissingExpirationCode_HardStops()
    {
        var rule = new ModificationRule
        {
            ModificationCode = "THAW-TEST",
            SourceProductTypeId = 1,
            TargetProductTypeId = 2,
            ModificationType = ModificationType.Thaw,
            ExpirationModificationCodeId = 0
        };

        var eval = ModificationRuleValidator.Validate(rule, duplicateActiveTriple: false, sourceProductActive: true, targetProductActive: true);

        Assert.True(eval.IsHardStopped);
        Assert.Contains(eval.HardStops, r => r.Code == "MODRULE.EXPCODE.REQUIRED");
    }

    [Fact]
    public void ModificationRule_InactiveExpirationCode_HardStops()
    {
        var rule = ValidModificationRule();

        var eval = ModificationRuleValidator.Validate(rule, duplicateActiveTriple: false, sourceProductActive: true, targetProductActive: true, expirationCodeActive: false);

        Assert.True(eval.IsHardStopped);
        Assert.Contains(eval.HardStops, r => r.Code == "MODRULE.EXPCODE.INACTIVE");
    }

    [Fact]
    public void ModificationRule_DuplicateActiveTriple_HardStops()
    {
        var rule = ValidModificationRule();
        rule.ModificationType = ModificationType.Pool;

        var eval = ModificationRuleValidator.Validate(rule, duplicateActiveTriple: true, sourceProductActive: true, targetProductActive: true, expirationCodeActive: true);

        Assert.True(eval.IsHardStopped);
        Assert.Contains(eval.HardStops, r => r.Code == "MODRULE.TRIPLE.DUPLICATE");
    }

    [Fact]
    public void ModificationRule_InactiveSourceOrTargetProduct_HardStops()
    {
        var rule = ValidModificationRule();
        rule.ModificationType = ModificationType.VolumeReduction;

        var eval = ModificationRuleValidator.Validate(rule, duplicateActiveTriple: false, sourceProductActive: false, targetProductActive: false, expirationCodeActive: true);

        Assert.True(eval.IsHardStopped);
        Assert.Contains(eval.HardStops, r => r.Code == "MODRULE.SOURCE.INACTIVE");
        Assert.Contains(eval.HardStops, r => r.Code == "MODRULE.TARGET.INACTIVE");
    }

    [Fact]
    public void ModificationRule_DivideSameSourceAndTarget_Warns()
    {
        var rule = ValidModificationRule();
        rule.TargetProductTypeId = 1;
        rule.ModificationType = ModificationType.Divide;

        var eval = ModificationRuleValidator.Validate(rule, duplicateActiveTriple: false, sourceProductActive: true, targetProductActive: true, expirationCodeActive: true);

        Assert.False(eval.IsHardStopped);
        Assert.Contains(eval.Warnings, r => r.Code == "MODRULE.SAMEPRODUCT");
    }

    [Fact]
    public void ModificationRule_IrradiateSameSourceAndTarget_DoesNotWarn()
    {
        var rule = ValidModificationRule();
        rule.TargetProductTypeId = 1;

        var eval = ModificationRuleValidator.Validate(rule, duplicateActiveTriple: false, sourceProductActive: true, targetProductActive: true, expirationCodeActive: true);

        Assert.False(eval.IsHardStopped);
        Assert.DoesNotContain(eval.Warnings, r => r.Code == "MODRULE.SAMEPRODUCT");
    }

    [Fact]
    public void ModificationRule_MissingCode_HardStops()
    {
        var rule = ValidModificationRule();
        rule.ModificationCode = " ";

        var eval = ModificationRuleValidator.Validate(rule, duplicateActiveTriple: false, sourceProductActive: true, targetProductActive: true, expirationCodeActive: true);

        Assert.True(eval.IsHardStopped);
        Assert.Contains(eval.HardStops, r => r.Code == "MODRULE.CODE.REQUIRED");
    }

    [Fact]
    public void ModificationRule_DuplicateCode_HardStops()
    {
        var rule = ValidModificationRule();

        var eval = ModificationRuleValidator.Validate(rule, duplicateActiveTriple: false, sourceProductActive: true, targetProductActive: true, expirationCodeActive: true, duplicateCode: true);

        Assert.True(eval.IsHardStopped);
        Assert.Contains(eval.HardStops, r => r.Code == "MODRULE.CODE.DUPLICATE");
    }

    [Fact]
    public void ExpirationModificationCode_Valid_Passes()
    {
        var code = new ExpirationModificationCode
        {
            Code = "24H",
            OffsetAmount = 24,
            OffsetUnit = ExpirationOffsetUnit.Hours,
            RelativeTo = ExpirationRelativeTo.ModificationDateTime
        };

        var eval = ExpirationModificationCodeValidator.Validate(code, duplicateActiveCode: false);

        Assert.False(eval.IsHardStopped);
    }

    [Fact]
    public void ExpirationModificationCode_MissingCodeAndZeroAmount_HardStops()
    {
        var code = new ExpirationModificationCode { Code = "", OffsetAmount = 0 };

        var eval = ExpirationModificationCodeValidator.Validate(code, duplicateActiveCode: false);

        Assert.True(eval.IsHardStopped);
        Assert.Contains(eval.HardStops, r => r.Code == "EXPCODE.CODE.REQUIRED");
        Assert.Contains(eval.HardStops, r => r.Code == "EXPCODE.AMOUNT.INVALID");
    }

    [Fact]
    public void ExpirationModificationCode_DuplicateActiveCode_HardStops()
    {
        var code = new ExpirationModificationCode
        {
            Code = "28D",
            OffsetAmount = 28,
            OffsetUnit = ExpirationOffsetUnit.Days,
            RelativeTo = ExpirationRelativeTo.ModificationDateTime
        };

        var eval = ExpirationModificationCodeValidator.Validate(code, duplicateActiveCode: true);

        Assert.True(eval.IsHardStopped);
        Assert.Contains(eval.HardStops, r => r.Code == "EXPCODE.CODE.DUPLICATE");
    }

    private static ModificationRule ValidModificationRule() => new()
    {
        ModificationCode = "IRR-RBC-LR",
        SourceProductTypeId = 1,
        TargetProductTypeId = 2,
        ModificationType = ModificationType.Irradiate,
        ExpirationModificationCodeId = 1
    };

    private static RuleDefinition NeonatalRule() => new()
    {
        Code = "NEO-TS",
        Name = "Neonatal type and screen",
        Level = RuleLevel.Order,
        ConditionExpression = "patient.ageDays < 1 AND order.hasTest('TS')",
        ActionExpression = "cancelTest('TS'); addTest('TSNEO')"
    };

    private static RuleDefinition WeakDRule() => new()
    {
        Code = "ABORH-WEAKD",
        Name = "Weak D on Rh negative",
        Level = RuleLevel.Test,
        ConditionExpression =
            "test.code = 'ABORH' AND test.interpretation IN ('A Negative','B Negative','O Negative','AB Negative')",
        ActionExpression = "addTest('WEAKD')"
    };

    [Fact]
    public void RuleDefinition_OrderExample_Passes()
    {
        var eval = RuleDefinitionValidator.Validate(NeonatalRule(), duplicateActiveCode: false);

        Assert.False(eval.IsHardStopped);
        Assert.True(eval.IsAllowed);
    }

    [Fact]
    public void RuleDefinition_TestExample_Passes()
    {
        var eval = RuleDefinitionValidator.Validate(WeakDRule(), duplicateActiveCode: false);

        Assert.False(eval.IsHardStopped);
    }

    [Fact]
    public void RuleDefinition_MissingFields_HardStops()
    {
        var eval = RuleDefinitionValidator.Validate(
            new RuleDefinition { Code = "", Name = "" },
            duplicateActiveCode: false);

        Assert.True(eval.IsHardStopped);
        Assert.Contains(eval.HardStops, r => r.Code == "RULE.CODE.REQUIRED");
        Assert.Contains(eval.HardStops, r => r.Code == "RULE.NAME.REQUIRED");
        Assert.Contains(eval.HardStops, r => r.Code == "RULE.CONDITION.REQUIRED");
        Assert.Contains(eval.HardStops, r => r.Code == "RULE.ACTION.REQUIRED");
    }

    [Fact]
    public void RuleDefinition_DuplicateCode_HardStops()
    {
        var eval = RuleDefinitionValidator.Validate(NeonatalRule(), duplicateActiveCode: true);

        Assert.Contains(eval.HardStops, r => r.Code == "RULE.CODE.DUPLICATE");
    }

    [Fact]
    public void RuleDefinition_BadConditionSyntax_HardStops()
    {
        var rule = NeonatalRule();
        rule.ConditionExpression = "patient.ageDays <";

        var eval = RuleDefinitionValidator.Validate(rule, duplicateActiveCode: false);

        Assert.Contains(eval.HardStops, r => r.Code == "RULE.CONDITION.SYNTAX");
    }

    [Fact]
    public void RuleDefinition_TestAttributeInOrderRule_HardStops()
    {
        var rule = NeonatalRule();
        rule.ConditionExpression = "test.interpretation = 'A Negative'";

        var eval = RuleDefinitionValidator.Validate(rule, duplicateActiveCode: false);

        Assert.Contains(eval.HardStops, r => r.Code == "RULE.CONDITION.ATTRIBUTE");
    }

    [Fact]
    public void RuleDefinition_UnknownAttribute_HardStops()
    {
        var rule = NeonatalRule();
        rule.ConditionExpression = "patient.height > 100";

        var eval = RuleDefinitionValidator.Validate(rule, duplicateActiveCode: false);

        Assert.Contains(eval.HardStops, r => r.Code == "RULE.CONDITION.ATTRIBUTE");
    }

    [Fact]
    public void RuleDefinition_BadActionSyntax_HardStops()
    {
        var rule = NeonatalRule();
        rule.ActionExpression = "addTest(TSNEO)";

        var eval = RuleDefinitionValidator.Validate(rule, duplicateActiveCode: false);

        Assert.Contains(eval.HardStops, r => r.Code == "RULE.ACTION.SYNTAX");
    }

    [Fact]
    public void RuleDefinition_BlockInTestRule_HardStops()
    {
        var rule = WeakDRule();
        rule.ActionExpression = "block('nope')";

        var eval = RuleDefinitionValidator.Validate(rule, duplicateActiveCode: false);

        Assert.Contains(eval.HardStops, r => r.Code == "RULE.ACTION.LEVEL");
    }

    [Fact]
    public void RuleDefinition_AddAndCancelSameTest_HardStops()
    {
        var rule = NeonatalRule();
        rule.ActionExpression = "cancelTest('TS'); addTest('ts')";

        var eval = RuleDefinitionValidator.Validate(rule, duplicateActiveCode: false);

        Assert.Contains(eval.HardStops, r => r.Code == "RULE.ACTION.SELF");
    }

    [Fact]
    public void RuleDefinition_UnknownTestCode_WarnsButDoesNotBlock()
    {
        var known = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "TS" };

        var eval = RuleDefinitionValidator.Validate(NeonatalRule(), duplicateActiveCode: false, known);

        Assert.False(eval.IsHardStopped);
        Assert.Contains(eval.Warnings, r => r.Code == "RULE.TEST.UNKNOWN" && r.Message.Contains("TSNEO"));
    }

    [Fact]
    public void RuleDefinition_KnownTestCodes_NoWarning()
    {
        var known = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "TS", "TSNEO" };

        var eval = RuleDefinitionValidator.Validate(NeonatalRule(), duplicateActiveCode: false, known);

        Assert.True(eval.IsAllowed);
    }
}
