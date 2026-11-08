using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    /// <summary>The shift handoff counts unidentified patients not yet merged and stored tubes due for discard.</summary>
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20261214000000_ShiftHandoffUnidentifiedTubes")]
    public class ShiftHandoffUnidentifiedTubes : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(name: "UnidentifiedPatients", table: "ShiftHandoffs", type: "int", nullable: false, defaultValue: 0);
            migrationBuilder.AddColumn<int>(name: "TubesDueForDiscard", table: "ShiftHandoffs", type: "int", nullable: false, defaultValue: 0);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "TubesDueForDiscard", table: "ShiftHandoffs");
            migrationBuilder.DropColumn(name: "UnidentifiedPatients", table: "ShiftHandoffs");
        }
    }
}
