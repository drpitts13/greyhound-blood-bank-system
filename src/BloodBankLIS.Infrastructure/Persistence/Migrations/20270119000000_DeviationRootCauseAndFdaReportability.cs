using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    /// <summary>A deviation records its root cause and the FDA reportability determination (end-user testing pass 73).</summary>
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20270119000000_DeviationRootCauseAndFdaReportability")]
    public class DeviationRootCauseAndFdaReportability : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(name: "RootCause", table: "Deviations", type: "nvarchar(4000)", maxLength: 4000, nullable: true);
            migrationBuilder.AddColumn<int>(name: "FdaReportability", table: "Deviations", type: "int", nullable: false, defaultValue: 0);
            migrationBuilder.AddColumn<DateOnly>(name: "FdaReportedOn", table: "Deviations", type: "date", nullable: true);
            migrationBuilder.AddColumn<string>(name: "FdaReportNumber", table: "Deviations", type: "nvarchar(100)", maxLength: 100, nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "RootCause", table: "Deviations");
            migrationBuilder.DropColumn(name: "FdaReportability", table: "Deviations");
            migrationBuilder.DropColumn(name: "FdaReportedOn", table: "Deviations");
            migrationBuilder.DropColumn(name: "FdaReportNumber", table: "Deviations");
        }
    }
}
