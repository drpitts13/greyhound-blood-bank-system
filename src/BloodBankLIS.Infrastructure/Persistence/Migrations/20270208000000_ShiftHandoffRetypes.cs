using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    /// <summary>End-user testing pass 143: units waiting for their ABO/Rh confirmation retype on the shift handoff.</summary>
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20270208000000_ShiftHandoffRetypes")]
    public class ShiftHandoffRetypes : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(name: "RetypesPending", table: "ShiftHandoffs", type: "int", nullable: false, defaultValue: 0);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "RetypesPending", table: "ShiftHandoffs");
        }
    }
}
