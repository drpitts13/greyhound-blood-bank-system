using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    /// <summary>The shift handoff also counts open antibody-identification workups and those waiting for review.</summary>
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20261222000000_ShiftHandoffAntibodyWorkups")]
    public class ShiftHandoffAntibodyWorkups : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(name: "AntibodyWorkupsOpen", table: "ShiftHandoffs", type: "int", nullable: false, defaultValue: 0);
            migrationBuilder.AddColumn<int>(name: "AntibodyWorkupsToReview", table: "ShiftHandoffs", type: "int", nullable: false, defaultValue: 0);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "AntibodyWorkupsOpen", table: "ShiftHandoffs");
            migrationBuilder.DropColumn(name: "AntibodyWorkupsToReview", table: "ShiftHandoffs");
        }
    }
}
