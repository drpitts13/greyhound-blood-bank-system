using BloodBankLIS.Infrastructure.Reporting;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// End-user testing pass 279: reinstalls the report views so <c>rpt_blood_order_turnaround</c> reads a needed-by time
    /// passed with no unit ready as missed. Development SQLite installs the views from the seeder.
    /// </summary>
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20270217000000_ReadyByNeededByMissed")]
    public class ReadyByNeededByMissed : Migration
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
