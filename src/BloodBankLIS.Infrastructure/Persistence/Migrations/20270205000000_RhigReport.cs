using BloodBankLIS.Infrastructure.Persistence;
using BloodBankLIS.Infrastructure.Reporting;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    /// <summary>End-user testing pass 121: installs <c>rpt_rhig</c>. Development SQLite installs the view from the seeder.</summary>
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20270205000000_RhigReport")]
    public class RhigReport : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            ReportViewSql.Apply(sql => migrationBuilder.Sql(sql), migrationBuilder.ActiveProvider);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP VIEW IF EXISTS rpt_rhig");
        }
    }
}
