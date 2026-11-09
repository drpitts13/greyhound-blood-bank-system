using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    /// <summary>The shift handoff also counts active massive transfusion protocols left idle (OCD-076).</summary>
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20261219000000_ShiftHandoffActiveMtpIdle")]
    public class ShiftHandoffActiveMtpIdle : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(name: "ActiveMtpIdle", table: "ShiftHandoffs", type: "int", nullable: false, defaultValue: 0);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "ActiveMtpIdle", table: "ShiftHandoffs");
        }
    }
}
