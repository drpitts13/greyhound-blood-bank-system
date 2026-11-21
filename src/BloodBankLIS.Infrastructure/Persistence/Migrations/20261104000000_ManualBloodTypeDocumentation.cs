using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    /// <summary>OCD-048: where a hand-recorded ABO/Rh came from, so only documented entries count as determinations.</summary>
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20261104000000_ManualBloodTypeDocumentation")]
    public class ManualBloodTypeDocumentation : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(name: "ManualDocumentation", table: "PatientBloodTypeHistory", type: "int", nullable: true);
            migrationBuilder.AddColumn<string>(name: "ManualDocumentationReference", table: "PatientBloodTypeHistory", type: "nvarchar(300)", maxLength: 300, nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "ManualDocumentationReference", table: "PatientBloodTypeHistory");
            migrationBuilder.DropColumn(name: "ManualDocumentation", table: "PatientBloodTypeHistory");
        }
    }
}
