using BloodBankLIS.Application.Admin;

namespace BloodBankLIS.Application.Tests;

// TEST-BB-1279 (end-user testing pass 254): an instrument message's rule trace is read as rows; the same rule and change
// in a row is counted once; a trace that is not the expected JSON gives no rows.
public class InstrumentRuleTraceViewTests
{
    [Fact]
    public void Rows_ReadTheTrace_AndCountRepeats()
    {
        const string zero = """{ "ruleName": "Numeric 0", "condition": "message.subtest_result = 0", "action": "set('subtest_result','0')", "changes": { "subtest_result": "0" } }""";
        const string pos = """{ "ruleName": "B Pos", "condition": "message.test_interp = 'B Pos'", "action": "set('test_interp','B|Positive')", "changes": { "test_interp": "B|Positive" } }""";
        var rows = InstrumentRuleTraceView.Rows($"[{zero},{zero},{zero},{pos},{zero}]");

        Assert.Equal(3, rows.Count);
        Assert.Equal(new InstrumentRuleTraceRow("Numeric 0", "message.subtest_result = 0", "subtest_result → 0", 3), rows[0]);
        Assert.Equal(new InstrumentRuleTraceRow("B Pos", "message.test_interp = 'B Pos'", "test_interp → B|Positive", 1), rows[1]);
        Assert.Equal(1, rows[2].Times);
    }

    [Fact]
    public void Rows_AreEmpty_ForNoTraceOrUnexpectedText()
    {
        Assert.Empty(InstrumentRuleTraceView.Rows(null));
        Assert.Empty(InstrumentRuleTraceView.Rows("[]"));
        Assert.Empty(InstrumentRuleTraceView.Rows("not json"));
        Assert.Empty(InstrumentRuleTraceView.Rows("{\"ruleName\":\"x\"}"));
    }
}
