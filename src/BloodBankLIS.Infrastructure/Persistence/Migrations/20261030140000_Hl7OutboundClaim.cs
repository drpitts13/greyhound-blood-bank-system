using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Outbound HL7 senders claim a message (Status = Sending, ClaimedUtc) with a
    /// conditional UPDATE before transmitting, so it can never go out twice.
    /// </summary>
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20261030140000_Hl7OutboundClaim")]
    public class Hl7OutboundClaim : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ClaimedUtc",
                table: "HL7Messages",
                type: "datetime2",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ClaimedUtc",
                table: "HL7Messages");
        }
    }
}
