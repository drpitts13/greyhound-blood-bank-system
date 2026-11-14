using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    /// <summary>The shift handoff also counts outbound HL7 messages that have waited too long without being sent.</summary>
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20261224000000_ShiftHandoffHl7OutboundNotSent")]
    public class ShiftHandoffHl7OutboundNotSent : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(name: "Hl7OutboundNotSent", table: "ShiftHandoffs", type: "int", nullable: false, defaultValue: 0);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "Hl7OutboundNotSent", table: "ShiftHandoffs");
        }
    }
}
