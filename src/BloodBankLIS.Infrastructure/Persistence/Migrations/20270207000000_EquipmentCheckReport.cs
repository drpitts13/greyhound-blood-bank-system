using BloodBankLIS.Infrastructure.Persistence;
using BloodBankLIS.Infrastructure.Reporting;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    /// <summary>End-user testing pass 140: installs <c>rpt_equipment_checks</c>. Development SQLite installs the view from the seeder.</summary>
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20270207000000_EquipmentCheckReport")]
    public class EquipmentCheckReport : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            ReportViewSql.Apply(sql => migrationBuilder.Sql(sql), migrationBuilder.ActiveProvider);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP VIEW IF EXISTS rpt_equipment_checks");
        }
    }
}
