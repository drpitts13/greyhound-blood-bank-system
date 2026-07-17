using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules.Config;

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
}
