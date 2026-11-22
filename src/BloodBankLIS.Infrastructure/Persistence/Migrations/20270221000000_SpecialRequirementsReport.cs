using BloodBankLIS.Infrastructure.Persistence;
using BloodBankLIS.Infrastructure.Reporting;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    /// <summary>End-user testing pass 329: installs <c>rpt_special_requirements</c>. Development SQLite installs the view from the seeder.</summary>
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20270221000000_SpecialRequirementsReport")]
    public class SpecialRequirementsReport : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            ReportViewSql.Apply(sql => migrationBuilder.Sql(sql), migrationBuilder.ActiveProvider);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP VIEW IF EXISTS rpt_special_requirements");
        }
    }
}
