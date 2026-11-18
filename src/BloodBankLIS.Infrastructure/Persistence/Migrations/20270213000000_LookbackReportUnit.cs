using BloodBankLIS.Infrastructure.Reporting;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// End-user testing pass 246: reinstalls the report views so <c>rpt_lookback</c> names the unit transfused
    /// and the date the notification was opened. Development SQLite installs the views from the seeder.
    /// </summary>
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20270213000000_LookbackReportUnit")]
    public class LookbackReportUnit : Migration
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
