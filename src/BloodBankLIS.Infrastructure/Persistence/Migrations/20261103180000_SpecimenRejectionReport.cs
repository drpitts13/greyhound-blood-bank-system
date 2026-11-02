using BloodBankLIS.Infrastructure.Persistence;
using BloodBankLIS.Infrastructure.Reporting;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Records when and by whom a specimen was rejected, and installs <c>rpt_specimen_rejections</c>.
    /// Development SQLite adds the columns in the bootstrap and installs the view from the seeder.
    /// </summary>
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20261103180000_SpecimenRejectionReport")]
    public class SpecimenRejectionReport : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(name: "RejectedUtc", table: "Specimens", type: "datetime2", nullable: true);
            migrationBuilder.AddColumn<string>(name: "RejectedBy", table: "Specimens", type: "nvarchar(100)", maxLength: 100, nullable: true);
            ReportViewSql.Apply(sql => migrationBuilder.Sql(sql), migrationBuilder.ActiveProvider);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP VIEW IF EXISTS rpt_specimen_rejections");
            migrationBuilder.DropColumn(name: "RejectedBy", table: "Specimens");
            migrationBuilder.DropColumn(name: "RejectedUtc", table: "Specimens");
        }
    }
}
