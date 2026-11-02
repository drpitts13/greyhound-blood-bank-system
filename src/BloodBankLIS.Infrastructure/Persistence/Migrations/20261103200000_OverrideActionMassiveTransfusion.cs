using BloodBankLIS.Infrastructure.Persistence;
using BloodBankLIS.Infrastructure.Reporting;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Re-installs the report views so <c>rpt_overrides</c> names the new MassiveTransfusion override action.
    /// No column changes: the action is stored as its integer value.
    /// </summary>
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20261103200000_OverrideActionMassiveTransfusion")]
    public class OverrideActionMassiveTransfusion : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            ReportViewSql.Apply(sql => migrationBuilder.Sql(sql), migrationBuilder.ActiveProvider);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            ReportViewSql.Apply(sql => migrationBuilder.Sql(sql), migrationBuilder.ActiveProvider);
        }
    }
}
