using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    /// <summary>An infant's chart linked to the mother's chart.</summary>
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20261213000000_MotherInfantLink")]
    public class MotherInfantLink : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(name: "MotherPatientId", table: "Patients", type: "bigint", nullable: true);
            migrationBuilder.CreateIndex(name: "IX_Patients_MotherPatientId", table: "Patients", column: "MotherPatientId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "IX_Patients_MotherPatientId", table: "Patients");
            migrationBuilder.DropColumn(name: "MotherPatientId", table: "Patients");
        }
    }
}
