using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    /// <summary>Each shift handoff also counts products below their minimum stock level.</summary>
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20261119000000_ShiftHandoffLowStock")]
    public class ShiftHandoffLowStock : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(name: "ProductsBelowMinimum", table: "ShiftHandoffs", type: "int", nullable: false, defaultValue: 0);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "ProductsBelowMinimum", table: "ShiftHandoffs");
        }
    }
}
