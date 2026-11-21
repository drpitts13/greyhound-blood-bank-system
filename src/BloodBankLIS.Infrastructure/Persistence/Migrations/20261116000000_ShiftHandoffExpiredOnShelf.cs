using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    /// <summary>Each shift handoff also counts expired units still recorded on a shelf, to pull and discard.</summary>
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20261116000000_ShiftHandoffExpiredOnShelf")]
    public class ShiftHandoffExpiredOnShelf : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(name: "ExpiredOnShelf", table: "ShiftHandoffs", type: "int", nullable: false, defaultValue: 0);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "ExpiredOnShelf", table: "ShiftHandoffs");
        }
    }
}
