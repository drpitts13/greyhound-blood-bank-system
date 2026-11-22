using BloodBankLIS.Infrastructure.Reporting;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// End-user testing pass 251: reinstalls the report views so <c>rpt_overrides</c> says who authorized each row
    /// (for an MTP issue without an override, the physician who called the protocol). Development SQLite installs the views from the seeder.
    /// </summary>
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20270215000000_OverridesReportAuthorizer")]
    public class OverridesReportAuthorizer : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            ReportViewSql.Apply(sql => migrationBuilder.Sql(sql), migrationBuilder.ActiveProvider);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
