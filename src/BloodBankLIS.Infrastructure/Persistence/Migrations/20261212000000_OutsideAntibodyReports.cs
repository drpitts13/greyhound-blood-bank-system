using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    /// <summary>Antibodies reported by another facility: who reported them and when they were identified there.</summary>
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20261212000000_OutsideAntibodyReports")]
    public class OutsideAntibodyReports : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(name: "ReportingFacility", table: "AntibodyHistory", type: "nvarchar(200)", maxLength: 200, nullable: true);
            migrationBuilder.AddColumn<DateOnly>(name: "IdentifiedOn", table: "AntibodyHistory", type: "date", nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "IdentifiedOn", table: "AntibodyHistory");
            migrationBuilder.DropColumn(name: "ReportingFacility", table: "AntibodyHistory");
        }
    }
}
