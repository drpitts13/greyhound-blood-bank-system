using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Domain.Rules.Config;

namespace BloodBankLIS.Domain.Tests;

/// <summary>TEST-BB-034 — Crossmatch settings validator and serologic attach choice.</summary>
public class CrossmatchSettingsValidatorTests
{
    [Fact]
    public void Validate_ValidSettings_Passes()
    {
        var settings = CrossmatchSettings.CreateDefault();
        var evaluation = CrossmatchSettingsValidator.Validate(
            settings,
            "Initial catalog",
            Active("XM", ResultValueType.Crossmatch),
            Active("CXM", ResultValueType.ComplexCrossmatch),
            Active("EXM", ResultValueType.Crossmatch));

        Assert.False(evaluation.IsHardStopped);
    }

    [Fact]
    public void Validate_MissingReason_IsHardStop()
    {
        var evaluation = CrossmatchSettingsValidator.Validate(
            CrossmatchSettings.CreateDefault(),
            "",
            Active("XM", ResultValueType.Crossmatch),
            Active("CXM", ResultValueType.ComplexCrossmatch),
            Active("EXM", ResultValueType.Crossmatch));

        Assert.Contains(evaluation.HardStops, r => r.Code == CrossmatchSettingsValidator.ReasonRequiredCode);
    }

    [Fact]
    public void Validate_ElectronicComplexTest_IsHardStop()
    {
        var settings = CrossmatchSettings.CreateDefault();
        settings.ElectronicCrossmatchTestCode = "CXM";
        var evaluation = CrossmatchSettingsValidator.Validate(
            settings,
            "Try complex EXM",
            Active("XM", ResultValueType.Crossmatch),
            Active("CXM", ResultValueType.ComplexCrossmatch),
            Active("CXM", ResultValueType.ComplexCrossmatch));

        Assert.Contains(evaluation.HardStops, r => r.Code == CrossmatchSettingsValidator.ElectronicTestCode);
    }

    [Fact]
    public void Validate_ZeroMinimums_AreHardStops()
    {
        var settings = CrossmatchSettings.CreateDefault();
        settings.ElectronicXmMinimumVisits = 0;
        settings.ElectronicXmMinimumSpecimens = 0;
        settings.ElectronicXmMinimumTests = 0;
        var evaluation = CrossmatchSettingsValidator.Validate(
            settings,
            "Bad counts",
            Active("XM", ResultValueType.Crossmatch),
            Active("CXM", ResultValueType.ComplexCrossmatch),
            Active("EXM", ResultValueType.Crossmatch));

        Assert.Contains(evaluation.HardStops, r => r.Code == CrossmatchSettingsValidator.MinVisitsCode);
        Assert.Contains(evaluation.HardStops, r => r.Code == CrossmatchSettingsValidator.MinSpecimensCode);
        Assert.Contains(evaluation.HardStops, r => r.Code == CrossmatchSettingsValidator.MinTestsCode);
    }

    [Fact]
    public void AttachmentRule_SkipsSerologic_WhenExmEligible()
    {
        var code = CrossmatchAttachmentRule.ChooseSerologicTestCode(true, false, "XM", "CXM");
        Assert.Null(code);
    }

    [Fact]
    public void AttachmentRule_UsesPositiveHistoryTest_WhenComplexRequired()
    {
        var code = CrossmatchAttachmentRule.ChooseSerologicTestCode(false, true, "XM", "CXM");
        Assert.Equal("CXM", code);
    }

    [Fact]
    public void AttachmentRule_UsesNegativeHistoryTest_WhenNoHistory()
    {
        var code = CrossmatchAttachmentRule.ChooseSerologicTestCode(false, false, "XM", "CXM");
        Assert.Equal("XM", code);
    }

    private static TestDefinition Active(string code, ResultValueType type) => new()
    {
        Code = code,
        Name = code,
        ResultValueType = type,
        IsActive = true,
        IsDraft = false
    };
}
