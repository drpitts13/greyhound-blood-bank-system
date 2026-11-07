using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    /// <summary>A patient's race and ethnicity (local codes), from registration or the ADT feed.</summary>
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20261124000000_PatientRaceEthnicity")]
    public class PatientRaceEthnicity : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(name: "Race", table: "Patients", type: "nvarchar(20)", maxLength: 20, nullable: true);
            migrationBuilder.AddColumn<string>(name: "Ethnicity", table: "Patients", type: "nvarchar(20)", maxLength: 20, nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "Race", table: "Patients");
            migrationBuilder.DropColumn(name: "Ethnicity", table: "Patients");
        }
    }
}
