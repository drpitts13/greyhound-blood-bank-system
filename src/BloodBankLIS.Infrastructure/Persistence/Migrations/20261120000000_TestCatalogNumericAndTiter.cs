using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Test definitions can describe numeric results (units, decimal places, reportable, reference, and critical limits)
    /// and titers (dilution series, phase, endpoint grade, scoring); numeric results carry an abnormal flag.
    /// </summary>
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20261120000000_TestCatalogNumericAndTiter")]
    public class TestCatalogNumericAndTiter : Migration
    {
        private static readonly string[] Limits = ["ReportableLow", "ReportableHigh", "ReferenceLow", "ReferenceHigh", "CriticalLow", "CriticalHigh"];

        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(name: "ResultUnits", table: "TestDefinitions", type: "nvarchar(50)", maxLength: 50, nullable: true);
            migrationBuilder.AddColumn<int>(name: "DecimalPlaces", table: "TestDefinitions", type: "int", nullable: true);
            foreach (var limit in Limits)
            {
                migrationBuilder.AddColumn<decimal>(name: limit, table: "TestDefinitions", type: "decimal(18,4)", precision: 18, scale: 4, nullable: true);
            }

            migrationBuilder.AddColumn<int>(name: "TiterStartDilution", table: "TestDefinitions", type: "int", nullable: true);
            migrationBuilder.AddColumn<int>(name: "TiterEndDilution", table: "TestDefinitions", type: "int", nullable: true);
            migrationBuilder.AddColumn<string>(name: "TiterPhaseCode", table: "TestDefinitions", type: "nvarchar(50)", maxLength: 50, nullable: true);
            migrationBuilder.AddColumn<string>(name: "TiterEndpointMinGrade", table: "TestDefinitions", type: "nvarchar(10)", maxLength: 10, nullable: true);
            migrationBuilder.AddColumn<bool>(name: "TiterScored", table: "TestDefinitions", type: "bit", nullable: false, defaultValue: false);
            migrationBuilder.AddColumn<string>(name: "AbnormalFlag", table: "TestResults", type: "nvarchar(10)", maxLength: 10, nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "AbnormalFlag", table: "TestResults");
            foreach (var column in new[] { "ResultUnits", "DecimalPlaces", "TiterStartDilution", "TiterEndDilution", "TiterPhaseCode", "TiterEndpointMinGrade", "TiterScored" }.Concat(Limits))
            {
                migrationBuilder.DropColumn(name: column, table: "TestDefinitions");
            }
        }
    }
}
