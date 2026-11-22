using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules.Config;

namespace BloodBankLIS.Domain.Tests;

// TEST-BB-518: numeric and titer test definitions are refused when their limits or dilution series do not make sense.
public class TestDefinitionNumericTiterValidatorTests
{
    private static readonly Dictionary<string, PhaseDefinition> Phases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["AHG"] = new PhaseDefinition { Code = "AHG", Name = "AHG" }
    };

    private static TestDefinition Numeric() => new()
    {
        Code = "FMHQ", Name = "Fetal bleed", ResultValueType = ResultValueType.Numeric, ResultUnits = "%", DecimalPlaces = 2,
        ReportableLow = 0, ReportableHigh = 100, ReferenceLow = 0, ReferenceHigh = 0.3m, CriticalHigh = 5
    };

    private static TestDefinition Titer() => new()
    {
        Code = "ABTITER", Name = "Antibody titer", ResultValueType = ResultValueType.Titer,
        TiterStartDilution = 1, TiterEndDilution = 2048, TiterPhaseCode = "AHG", TiterEndpointMinGrade = "1+"
    };

    private static IEnumerable<string> Stops(TestDefinition d) =>
        TestDefinitionValidator.Validate(d, false, phasesByCode: Phases).HardStops.Select(r => r.Code);

    [Fact]
    public void WellFormedNumericAndTiter_Pass()
    {
        Assert.Empty(Stops(Numeric()));
        Assert.Empty(Stops(Titer()));
    }

    [Fact]
    public void BadNumericLimits_AreRefused()
    {
        var d = Numeric();
        d.DecimalPlaces = 5;
        Assert.Contains("TESTDEF.NUM.DECIMALS", Stops(d));

        d = Numeric();
        d.ReferenceLow = 0.5m;
        Assert.Contains("TESTDEF.NUM.RANGE", Stops(d));

        d = Numeric();
        d.ReferenceHigh = 150;
        Assert.Contains("TESTDEF.NUM.REFERENCE", Stops(d));

        d = Numeric();
        d.CriticalHigh = 0.2m;
        Assert.Contains("TESTDEF.NUM.CRITICAL", Stops(d));
    }

    [Fact]
    public void BadTiterSetup_IsRefused()
    {
        var d = Titer();
        d.TiterEndDilution = 1000;
        Assert.Contains("TESTDEF.TITER.SERIES", Stops(d));

        d = Titer();
        d.TiterStartDilution = null;
        Assert.Contains("TESTDEF.TITER.SERIES", Stops(d));

        d = Titer();
        d.TiterEndpointMinGrade = "0";
        Assert.Contains("TESTDEF.TITER.ENDPOINT", Stops(d));

        d = Titer();
        d.TiterPhaseCode = "37C";
        Assert.Contains("TESTDEF.TITER.PHASE", Stops(d));
    }
}
