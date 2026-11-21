using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    /// <summary>The shift handoff counts overdue final dispositions and outside antibodies to confirm (Cycle 679).</summary>
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20270104000000_ShiftHandoffDispositionAndOutside")]
    public class ShiftHandoffDispositionAndOutside : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(name: "DispositionOverdue", table: "ShiftHandoffs", type: "int", nullable: false, defaultValue: 0);
            migrationBuilder.AddColumn<int>(name: "OutsideAntibodiesToConfirm", table: "ShiftHandoffs", type: "int", nullable: false, defaultValue: 0);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "DispositionOverdue", table: "ShiftHandoffs");
            migrationBuilder.DropColumn(name: "OutsideAntibodiesToConfirm", table: "ShiftHandoffs");
        }
    }
}
