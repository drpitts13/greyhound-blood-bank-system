using BloodBankLIS.Domain.Reporting;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Domain.Tests;

public class ReportSqlGuardTests
{
    [Fact]
    public void Select_FromReportingView_Passes()
    {
        var result = ReportSqlGuard.Evaluate("SELECT unit_number FROM rpt_inventory_on_hand");
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }

    [Fact]
    public void WithClause_AgainstReportingView_Passes()
    {
        var sql = """
            WITH recent AS (SELECT unit_number FROM rpt_inventory_on_hand)
            SELECT unit_number FROM recent
            """;
        var result = ReportSqlGuard.Evaluate(sql);
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }

    [Theory]
    [InlineData("INSERT INTO rpt_inventory_on_hand (unit_number) VALUES ('x')")]
    [InlineData("UPDATE rpt_inventory_on_hand SET unit_number = 'x'")]
    [InlineData("DELETE FROM rpt_inventory_on_hand")]
    [InlineData("DROP VIEW rpt_inventory_on_hand")]
    [InlineData("SELECT * INTO copy FROM rpt_inventory_on_hand")]
    [InlineData("EXEC sp_help")]
    public void WritesAndDdl_AreRejected(string sql)
    {
        var result = ReportSqlGuard.Evaluate(sql);
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
    }

    [Fact]
    public void SecondStatement_IsRejected()
    {
        var result = ReportSqlGuard.Evaluate("SELECT unit_number FROM rpt_inventory_on_hand; DELETE FROM Patients");
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
    }

    [Fact]
    public void Comment_IsRejected()
    {
        var result = ReportSqlGuard.Evaluate("SELECT unit_number FROM rpt_inventory_on_hand -- hide\nSELECT 1");
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
    }

    [Fact]
    public void ClinicalTable_IsRejected()
    {
        var result = ReportSqlGuard.Evaluate("SELECT MedicalRecordNumber FROM Patients");
        Assert.Equal(RuleSeverity.HardStop, result.Severity);
        Assert.Contains("Patients", result.Message);
    }

    [Fact]
    public void TrailingSemicolon_Passes()
    {
        var result = ReportSqlGuard.Evaluate("SELECT unit_number FROM rpt_discards;");
        Assert.Equal(RuleSeverity.Pass, result.Severity);
    }
}
