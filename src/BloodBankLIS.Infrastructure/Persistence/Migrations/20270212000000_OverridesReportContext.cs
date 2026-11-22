using BloodBankLIS.Infrastructure.Reporting;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// End-user testing pass 245: reinstalls the report views so <c>rpt_overrides</c> names an override's unit and patient
    /// through its issue, reservation or crossmatch. Development SQLite installs the views from the seeder.
    /// </summary>
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20270212000000_OverridesReportContext")]
    public class OverridesReportContext : Migration
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
