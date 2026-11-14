using BloodBankLIS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BloodBankLIS.Infrastructure.Persistence.Migrations
{
    /// <summary>Why an interface error was resolved: the reason given by hand, or the replay or resend that delivered it.</summary>
    [DbContext(typeof(BloodBankDbContext))]
    [Migration("20261107000000_InterfaceErrorResolutionNote")]
    public class InterfaceErrorResolutionNote : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(name: "ResolutionNote", table: "InterfaceErrorQueue", type: "nvarchar(500)", maxLength: 500, nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "ResolutionNote", table: "InterfaceErrorQueue");
        }
    }
}
