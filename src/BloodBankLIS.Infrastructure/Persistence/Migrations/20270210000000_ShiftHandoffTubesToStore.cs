using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    /// <summary>End-user testing pass 145: received tubes with no storage place on the shift handoff.</summary>
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20270210000000_ShiftHandoffTubesToStore")]
    public class ShiftHandoffTubesToStore : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(name: "TubesToStore", table: "ShiftHandoffs", type: "int", nullable: false, defaultValue: 0);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "TubesToStore", table: "ShiftHandoffs");
        }
    }
}
