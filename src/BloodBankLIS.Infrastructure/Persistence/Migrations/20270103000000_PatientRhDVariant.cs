using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    /// <summary>OCD-069: a weak D or partial D recorded for the patient (Cycle 675).</summary>
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20270103000000_PatientRhDVariant")]
    public class PatientRhDVariant : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(name: "RhDVariant", table: "Patients", type: "int", nullable: false, defaultValue: 0);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "RhDVariant", table: "Patients");
        }
    }
}
