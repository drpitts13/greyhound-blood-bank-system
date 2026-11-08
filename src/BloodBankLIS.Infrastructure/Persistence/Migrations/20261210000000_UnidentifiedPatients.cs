using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    /// <summary>Patients registered without identification for emergency care, until merged into the identified chart.</summary>
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20261210000000_UnidentifiedPatients")]
    public class UnidentifiedPatients : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(name: "Unidentified", table: "Patients", type: "bit", nullable: false, defaultValue: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "Unidentified", table: "Patients");
        }
    }
}
