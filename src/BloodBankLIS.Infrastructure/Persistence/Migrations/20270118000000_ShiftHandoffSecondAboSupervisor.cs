using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    /// <summary>The shift handoff counts second ABO/Rh due and supervisor queue items (end-user testing pass 71).</summary>
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20270118000000_ShiftHandoffSecondAboSupervisor")]
    public class ShiftHandoffSecondAboSupervisor : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(name: "SecondAboRhDue", table: "ShiftHandoffs", type: "int", nullable: false, defaultValue: 0);
            migrationBuilder.AddColumn<int>(name: "SupervisorQueueOpen", table: "ShiftHandoffs", type: "int", nullable: false, defaultValue: 0);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "SecondAboRhDue", table: "ShiftHandoffs");
            migrationBuilder.DropColumn(name: "SupervisorQueueOpen", table: "ShiftHandoffs");
        }
    }
}
