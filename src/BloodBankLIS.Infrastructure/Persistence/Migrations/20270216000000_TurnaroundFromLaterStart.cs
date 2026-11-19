using BloodBankLIS.Infrastructure.Reporting;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// End-user testing pass 278: reinstalls the report views so <c>rpt_test_turnaround</c> measures from receipt, or from
    /// the order when the tube was received before it was ordered. Development SQLite installs the views from the seeder.
    /// </summary>
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20270216000000_TurnaroundFromLaterStart")]
    public class TurnaroundFromLaterStart : Migration
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
