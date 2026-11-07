using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    /// <summary>Running and overdue transfusions, and incompatible retrospective crossmatches, counted on each shift handoff.</summary>
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20261103230000_ShiftHandoffTransfusionCounts")]
    public class ShiftHandoffTransfusionCounts : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(name: "TransfusionsRunning", table: "ShiftHandoffs", type: "int", nullable: false, defaultValue: 0);
            migrationBuilder.AddColumn<int>(name: "TransfusionsCompletionOverdue", table: "ShiftHandoffs", type: "int", nullable: false, defaultValue: 0);
            migrationBuilder.AddColumn<int>(name: "RetrospectiveIncompatible", table: "ShiftHandoffs", type: "int", nullable: false, defaultValue: 0);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "RetrospectiveIncompatible", table: "ShiftHandoffs");
            migrationBuilder.DropColumn(name: "TransfusionsCompletionOverdue", table: "ShiftHandoffs");
            migrationBuilder.DropColumn(name: "TransfusionsRunning", table: "ShiftHandoffs");
        }
    }
}
