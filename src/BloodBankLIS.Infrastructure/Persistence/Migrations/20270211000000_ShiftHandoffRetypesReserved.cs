using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    /// <summary>End-user testing pass 159: units to retype already reserved for patients, on the shift handoff.</summary>
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20270211000000_ShiftHandoffRetypesReserved")]
    public class ShiftHandoffRetypesReserved : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(name: "RetypesReserved", table: "ShiftHandoffs", type: "int", nullable: false, defaultValue: 0);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "RetypesReserved", table: "ShiftHandoffs");
        }
    }
}
